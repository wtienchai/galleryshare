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
using System.Linq;
using System.Threading.Tasks;
//using Microsoft.AspNet.SignalR;
using Voat.Data.Models;

namespace Voat.Utilities
{
    public class VotingComments
    {
        // submit comment upvote
        public static async Task UpvoteComment(int commentId, string userWhichUpvoted, string clientIpHash)
        {
            int result = CheckIfVotedComment(userWhichUpvoted, commentId);

            using (voatEntities db = new voatEntities())
            {
                Comment comment = db.Comments.Find(commentId);

                if (comment.Message.Anonymized)
                {
                    // do not execute voting, subverse is in anonymized mode
                    return;
                }

                switch (result)
                {
                    // never voted before
                    case 0:

                        if (comment.Name != userWhichUpvoted)
                        {
                            // check if this IP already voted on the same comment, abort voting if true
                            var ipVotedAlready = db.Commentvotingtrackers.Where(x => x.CommentId == commentId && x.ClientIpAddress == clientIpHash);
                            if (ipVotedAlready.Any()) return;

                            comment.Likes++;

                            // register upvote
                            var tmpVotingTracker = new Commentvotingtracker
                            {
                                CommentId = commentId,
                                UserName = userWhichUpvoted,
                                VoteStatus = 1,
                                Timestamp = DateTime.Now,
                                ClientIpAddress = clientIpHash
                            };
                            db.Commentvotingtrackers.Add(tmpVotingTracker);
                            await db.SaveChangesAsync();

                            Voting.SendVoteNotification(comment.Name, "upvote");
                        }

                        break;

                    // downvoted before, turn downvote to upvote
                    case -1:

                        if (comment.Name != userWhichUpvoted)
                        {
                            comment.Likes++;
                            comment.Dislikes--;

                            // register Turn DownVote To UpVote
                            var votingTracker = db.Commentvotingtrackers.FirstOrDefault(b => b.CommentId == commentId && b.UserName == userWhichUpvoted);

                            if (votingTracker != null)
                            {
                                votingTracker.VoteStatus = 1;
                                votingTracker.Timestamp = DateTime.Now;
                            }
                            await db.SaveChangesAsync();

                            Voting.SendVoteNotification(comment.Name, "downtoupvote");
                        }

                        break;

                    // upvoted before, reset
                    case 1:

                        comment.Likes--;
                        db.SaveChanges();

                        Voting.SendVoteNotification(comment.Name, "downvote");

                        await ResetCommentVote(userWhichUpvoted, commentId);

                        break;
                }
            }

        }

        // submit submission downvote
        public static async Task DownvoteComment(int commentId, string userWhichDownvoted, string clientIpHash)
        {
            int result = CheckIfVotedComment(userWhichDownvoted, commentId);

            using (voatEntities db = new voatEntities())
            {
                Comment comment = db.Comments.Find(commentId);

                // do not execute downvoting, subverse is in anonymized mode
                if (comment.Message.Anonymized)
                {
                    return;
                }

                // do not execute downvoting if user has insufficient CCP for target subverse
                if (Karma.CommentKarmaForSubverse(userWhichDownvoted, comment.Message.Subverse) < comment.Message.Subverses.minimumdownvoteccp)
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

                            // check if this IP already voted on the same comment, abort voting if true
                            var ipVotedAlready = db.Commentvotingtrackers.Where(x => x.CommentId == commentId && x.ClientIpAddress == clientIpHash);
                            if (ipVotedAlready.Any()) return;

                            comment.Dislikes++;

                            // register downvote
                            var tmpVotingTracker = new Commentvotingtracker
                            {
                                CommentId = commentId,
                                UserName = userWhichDownvoted,
                                VoteStatus = -1,
                                Timestamp = DateTime.Now,
                                ClientIpAddress = clientIpHash
                            };
                            db.Commentvotingtrackers.Add(tmpVotingTracker);
                            await db.SaveChangesAsync();

                            Voting.SendVoteNotification(comment.Name, "downvote");
                        }

                        break;

                    // upvoted before, turn upvote to downvote
                    case 1:
                        {
                            comment.Likes--;
                            comment.Dislikes++;

                            //register Turn DownVote To UpVote
                            var votingTracker = db.Commentvotingtrackers.FirstOrDefault(b => b.CommentId == commentId && b.UserName == userWhichDownvoted);

                            if (votingTracker != null)
                            {
                                votingTracker.VoteStatus = -1;
                                votingTracker.Timestamp = DateTime.Now;
                            }
                            await db.SaveChangesAsync();

                            Voting.SendVoteNotification(comment.Name, "uptodownvote");
                        }

                        break;

                    // downvoted before, reset
                    case -1:

                        comment.Dislikes--;
                        db.SaveChanges();
                        await ResetCommentVote(userWhichDownvoted, commentId);

                        Voting.SendVoteNotification(comment.Name, "upvote");

                        break;
                }
            }

        }

        // returns -1:downvoted, 1:upvoted, or 0:not voted
        public static int CheckIfVotedComment(string userToCheck, int commentId)
        {
            int intCheckResult = 0;

            using (var db = new voatEntities())
            {
                var checkResult = db.Commentvotingtrackers.FirstOrDefault(b => b.CommentId == commentId && b.UserName == userToCheck);

                intCheckResult = checkResult != null ? checkResult.VoteStatus.Value : 0;

                return intCheckResult;
            }

        }

        // a user has either upvoted or downvoted this submission earlier and wishes to reset the vote, delete the record
        public static async Task ResetCommentVote(string userWhichVoted, int commentId)
        {
            using (var db = new voatEntities())
            {
                var votingTracker = db.Commentvotingtrackers.FirstOrDefault(b => b.CommentId == commentId && b.UserName == userWhichVoted);

                if (votingTracker == null) return;
                db.Commentvotingtrackers.Remove(votingTracker);
                await db.SaveChangesAsync();
            }
        }

        public static async Task IncrementUserCcp(string userName)
        {
            using (var db = new voatEntities())
            {
                var userScp = db.Userscores.FirstOrDefault(x => x.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

                if (userScp == null)
                {
                    // initialize user score
                    var newUserScoreEntry = new Userscore
                    {
                        CCP = 1,
                        SCP = 0,
                        Username = userName
                    };
                    db.Userscores.Add(newUserScoreEntry);
                }
                else
                {
                    userScp.CCP++;
                }

                await db.SaveChangesAsync();
            }
        }

        public static async Task DecrementUserCcp(string userName)
        {
            using (var db = new voatEntities())
            {
                var userCcp = db.Userscores.FirstOrDefault(x => x.Username.Equals(userName, StringComparison.OrdinalIgnoreCase));

                if (userCcp == null)
                {
                    // initialize user score
                    var newUserScoreEntry = new Userscore
                    {
                        CCP = -1,
                        SCP = 0,
                        Username = userName
                    };
                    db.Userscores.Add(newUserScoreEntry);
                }
                else
                {
                    userCcp.CCP--;
                }

                await db.SaveChangesAsync();
            }
        }

        //This code is repeated in Voating.cs
        //// send SignalR realtime notification of incoming commentvote to the author
        //private static void SendVoteNotification(string userName, string notificationType)
        //{
        //    ////MIGRATION HACK
        //    //var hubContext = GlobalHost.ConnectionManager.GetHubContext<MessagingHub>();

        //    //switch (notificationType)
        //    //{
        //    //    case "downvote":
        //    //        {
        //    //            hubContext.Clients.User(userName).incomingDownvote(2);
        //    //        }
        //    //        break;
        //    //    case "upvote":
        //    //        {
        //    //            hubContext.Clients.User(userName).incomingUpvote(2);
        //    //        }
        //    //        break;
        //    //    case "downtoupvote":
        //    //        {
        //    //            hubContext.Clients.User(userName).incomingDownToUpvote(2);
        //    //        }
        //    //        break;
        //    //    case "uptodownvote":
        //    //        {
        //    //            hubContext.Clients.User(userName).incomingUpToDownvote(2);
        //    //        }
        //    //        break;
        //    //}
        //}
    }
}