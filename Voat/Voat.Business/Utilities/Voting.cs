/*
This source file is subject to version 3 of the GPL license, 
that is bundled with this package in the file LICENSE, and is 
available online at http://www.gnu.org/licenses/gpl.txt; 
you may not use this file except in compliance with the License. 

Software distributed under the License is distributed on an "AS IS" basis,
WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
the specific language governing rights and limitations under the License.

All portions of the code written by Voat are Copyright (c) 2014 Voat
All Rights Reserved.
*/

using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
//using Microsoft.AspNet.SignalR;
using Voat.Data.Models;

namespace Voat.Utilities
{
    public static class Voting
    {
        // returns -1:downvoted, 1:upvoted, 0:not voted
        public static int CheckIfVoted(string userToCheck, int messageId)
        {
            using (var db = new voatEntities())
            {
                var checkResult = db.Votingtrackers.Where(u => u.UserName == userToCheck && u.MessageId == messageId)
                        .AsNoTracking()
                        .FirstOrDefault();

                int intCheckResult = checkResult != null ? checkResult.VoteStatus.Value : 0;
                return intCheckResult;
            }
        }

        // a user has either upvoted or downvoted this submission earlier and wishes to reset the vote, delete the record
        public static async Task ResetMessageVote(string userWhichVoted, int messageId)
        {
            using (var db = new voatEntities())
            {
                var votingTracker = db.Votingtrackers.FirstOrDefault(b => b.MessageId == messageId && b.UserName == userWhichVoted);

                if (votingTracker == null) return;
                db.Votingtrackers.Remove(votingTracker);
                await db.SaveChangesAsync();

                // TODO: check if upvoted or downvoted before reset and update userscore accordingly
            }
        }

        // submit submission upvote
        public static async Task UpvoteSubmission(int submissionId, string userWhichUpvoted, string clientIp)
        {
            // user account voting check
            int result = CheckIfVoted(userWhichUpvoted, submissionId);

            using (var db = new voatEntities())
            {
                Message submission = db.Messages.Find(submissionId);

                if (submission.Anonymized)
                {
                    // do not execute voting, subverse is in anonymized mode
                    return;
                }

                switch (result)
                {
                    // never voted before
                    case 0:

                        if (submission.Name != userWhichUpvoted)
                        {
                            // check if this IP already voted on the same submission, abort voting if true
                            var ipVotedAlready = db.Votingtrackers.Where(x => x.MessageId == submissionId && x.ClientIpAddress == clientIp);
                            if (ipVotedAlready.Any()) return;

                            submission.Likes++;
                            double currentScore = submission.Likes - submission.Dislikes;
                            double submissionAge = Submissions.CalcSubmissionAgeDouble(submission.Date);
                            double newRank = Ranking.CalculateNewRank(submission.Rank, submissionAge, currentScore);
                            submission.Rank = newRank;

                            // register upvote
                            var tmpVotingTracker = new Votingtracker
                            {
                                MessageId = submissionId,
                                UserName = userWhichUpvoted,
                                VoteStatus = 1,
                                Timestamp = DateTime.Now,
                                ClientIpAddress = clientIp
                            };

                            db.Votingtrackers.Add(tmpVotingTracker);
                            await db.SaveChangesAsync();

                            await IncrementUserScp(submission.Name);

                            SendVoteNotification(submission.Name, "upvote");
                        }

                        break;

                    // downvoted before, turn downvote to upvote
                    case -1:

                        if (submission.Name != userWhichUpvoted)
                        {
                            submission.Likes++;
                            submission.Dislikes--;

                            double currentScore = submission.Likes - submission.Dislikes;
                            double submissionAge = Submissions.CalcSubmissionAgeDouble(submission.Date);
                            double newRank = Ranking.CalculateNewRank(submission.Rank, submissionAge, currentScore);
                            submission.Rank = newRank;

                            // register Turn DownVote To UpVote
                            var votingTracker = db.Votingtrackers.FirstOrDefault(b => b.MessageId == submissionId && b.UserName == userWhichUpvoted);

                            if (votingTracker != null)
                            {
                                votingTracker.VoteStatus = 1;
                                votingTracker.Timestamp = DateTime.Now;
                            }
                            await db.SaveChangesAsync();

                            // TODO: increment user score by 2

                            SendVoteNotification(submission.Name, "downtoupvote");
                        }

                        break;

                    // upvoted before, reset
                    case 1:
                        {
                            submission.Likes--;

                            double currentScore = submission.Likes - submission.Dislikes;
                            double submissionAge = Submissions.CalcSubmissionAgeDouble(submission.Date);
                            double newRank = Ranking.CalculateNewRank(submission.Rank, submissionAge, currentScore);

                            submission.Rank = newRank;
                            db.SaveChanges();

                            await ResetMessageVote(userWhichUpvoted, submissionId);

                            SendVoteNotification(submission.Name, "downvote");
                        }

                        break;
                }
            }

        }

        // submit submission downvote
        public static async Task DownvoteSubmission(int submissionId, string userWhichDownvoted, string clientIp)
        {
            int result = CheckIfVoted(userWhichDownvoted, submissionId);

            using (var db = new voatEntities())
            {
                Message submission = db.Messages.Find(submissionId);

                // do not execute downvoting if subverse is in anonymized mode
                if (submission.Anonymized)
                {
                    return;
                }
                
                // do not execute downvoting if user has insufficient CCP for target subverse
                if (Karma.CommentKarmaForSubverse(userWhichDownvoted, submission.Subverse) < submission.Subverses.minimumdownvoteccp)
                {
                    return;
                }

                switch (result)
                {
                    // never voted before
                    case 0:
                        {
                            // this user is downvoting more than upvoting, don't register the downvote
                            if (UserHelper.IsUserCommentVotingMeanie(userWhichDownvoted))
                            {
                                return;
                            }

                            // check if this IP already voted on the same submission, abort voting if true
                            var ipVotedAlready = db.Votingtrackers.Where(x => x.MessageId == submissionId && x.ClientIpAddress == clientIp);
                            if (ipVotedAlready.Any()) return;

                            submission.Dislikes++;

                            double currentScore = submission.Likes - submission.Dislikes;
                            double submissionAge = Submissions.CalcSubmissionAgeDouble(submission.Date);
                            double newRank = Ranking.CalculateNewRank(submission.Rank, submissionAge, currentScore);

                            submission.Rank = newRank;

                            // register downvote
                            var tmpVotingTracker = new Votingtracker
                            {
                                MessageId = submissionId,
                                UserName = userWhichDownvoted,
                                VoteStatus = -1,
                                Timestamp = DateTime.Now,
                                ClientIpAddress = clientIp
                            };
                            db.Votingtrackers.Add(tmpVotingTracker);
                            await db.SaveChangesAsync();

                            await DecrementUserScp(submission.Name);
                            SendVoteNotification(submission.Name, "downvote");
                        }

                        break;

                    // upvoted before, turn upvote to downvote
                    case 1:
                        {
                            submission.Likes--;
                            submission.Dislikes++;

                            double currentScore = submission.Likes - submission.Dislikes;
                            double submissionAge = Submissions.CalcSubmissionAgeDouble(submission.Date);
                            double newRank = Ranking.CalculateNewRank(submission.Rank, submissionAge, currentScore);

                            submission.Rank = newRank;

                            // register Turn DownVote To UpVote
                            var votingTracker = db.Votingtrackers.FirstOrDefault(b => b.MessageId == submissionId && b.UserName == userWhichDownvoted);

                            if (votingTracker != null)
                            {
                                votingTracker.VoteStatus = -1;
                                votingTracker.Timestamp = DateTime.Now;
                            }
                            await db.SaveChangesAsync();

                            SendVoteNotification(submission.Name, "uptodownvote");
                        }

                        break;

                    // downvoted before, reset
                    case -1:
                        {
                            submission.Dislikes--;

                            double currentScore = submission.Likes - submission.Dislikes;
                            double submissionAge = Submissions.CalcSubmissionAgeDouble(submission.Date);
                            double newRank = Ranking.CalculateNewRank(submission.Rank, submissionAge, currentScore);

                            submission.Rank = newRank;
                            db.SaveChanges();

                            ResetMessageVote(userWhichDownvoted, submissionId);

                            SendVoteNotification(submission.Name, "upvote");
                        }

                        break;
                }
            }
        }

        // send SignalR realtime notification of incoming vote to the author
        public static void SendVoteNotification(string userName, string notificationType)
        {
            //MIGRATION HACK: 
            //var hubContext = GlobalHost.ConnectionManager.GetHubContext<MessagingHub>();

            //switch (notificationType)
            //{
            //    case "downvote":
            //        {
            //            hubContext.Clients.User(userName).incomingDownvote(1);
            //        }
            //        break;
            //    case "upvote":
            //        {
            //            hubContext.Clients.User(userName).incomingUpvote(1);
            //        }
            //        break;
            //    case "downtoupvote":
            //        {
            //            hubContext.Clients.User(userName).incomingDownToUpvote(1);
            //        }
            //        break;
            //    case "uptodownvote":
            //        {
            //            hubContext.Clients.User(userName).incomingUpToDownvote(1);
            //        }
            //        break;
            //    case "commentdownvote":
            //        {
            //            hubContext.Clients.User(userName).incomingDownvote(2);
            //        }
            //        break;
            //    case "commentupvote":
            //        {
            //            hubContext.Clients.User(userName).incomingUpvote(2);
            //        }
            //        break;
            //    case "commentdowntoupvote":
            //        {
            //            hubContext.Clients.User(userName).incomingDownToUpvote(2);
            //        }
            //        break;
            //    case "commentuptodownvote":
            //        {
            //            hubContext.Clients.User(userName).incomingUpToDownvote(2);
            //        }
            //        break;
            //}
        }

        public static async Task IncrementUserScp(string userName)
        {
            using (var db = new voatEntities())
            {
                var userScp = db.Userscores.FirstOrDefault(x => x.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

                if (userScp == null)
                {
                    // initialize user score
                    var newUserScoreEntry = new Userscore
                    {
                        CCP = 0,
                        SCP = 1,
                        Username = userName
                    };
                    db.Userscores.Add(newUserScoreEntry);
                }
                else
                {
                    userScp.SCP++;
                }

                await db.SaveChangesAsync();
            }
        }

        public static async Task DecrementUserScp(string userName)
        {
            using (var db = new voatEntities())
            {
                var userScp = db.Userscores.FirstOrDefault(x => x.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

                if (userScp == null)
                {
                    // initialize user score
                    var newUserScoreEntry = new Userscore
                    {
                        CCP = 0,
                        SCP = -1,
                        Username = userName
                    };
                    db.Userscores.Add(newUserScoreEntry);
                }
                else
                {
                    userScp.SCP--;
                }

                await db.SaveChangesAsync();
            }
        }
    }
}