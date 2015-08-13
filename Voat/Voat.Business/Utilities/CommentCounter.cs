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
using Voat.Data.Models;

namespace Voat.Utilities
{

    //THIS IS A TEMPORARY DEADLOCK WORKAROUND (AND NOW CACHEABLE CONTENT)
    public static class CommentCounter
    {
        public static int CommentCount(int submissionID)
        {
            int count = 0;

            string cacheKey = String.Format("comment.count.{0}", submissionID).ToString();
            object data = CacheHandler.Retrieve(cacheKey);
            if (data == null)
            {

                data = CacheHandler.Register(cacheKey, new Func<object>(() =>
                {
                    using (voatEntities db = new voatEntities())
                    {
                        var cmd = db.Database.Connection.CreateCommand();
                        cmd.CommandText = "SELECT COUNT(*) FROM Comments WITH (NOLOCK) WHERE MessageID = @MessageID AND Name != 'deleted'";
                        var param = cmd.CreateParameter();
                        param.ParameterName = "MessageID";
                        param.DbType = System.Data.DbType.Int32;
                        param.Value = submissionID;
                        cmd.Parameters.Add(param);

                        if (cmd.Connection.State != System.Data.ConnectionState.Open)
                        {
                            cmd.Connection.Open();
                        }
                        return (int)cmd.ExecuteScalar();
                    }

                }), TimeSpan.FromMinutes(2), 1);

                count = (int)data;
            }
            else {
                count = (int)data;
            }
            return count;
        }
    }


}