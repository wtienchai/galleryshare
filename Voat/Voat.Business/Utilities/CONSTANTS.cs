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

namespace Voat.Utilities
{
    public static class CONSTANTS
    {
        public const string CONNECTION_LIVE = "voatEntities";
        public const string CONNECTION_READONLY = "voatEntitiesReadOnly";
        public const int DEFAULT_GUEST_PAGE_CACHE_MINUTES = 5;
        public const string USER_NAME_REGEX = "[a-zA-Z0-9]{1}[A-Za-z0-9-_]{1,19}";

    }
}