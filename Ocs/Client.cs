using System;

namespace CompuMaster.Ocs
{
    /// <summary>
    /// Open Collaboration Services OCS and WebDAV access client for OwnCloud, Nextcloud, etc.
    /// </summary>
    [Obsolete("Use OcsClient instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public class Client : OcsClient
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompuMaster.Ocs.OcsClient"/> class.
        /// </summary>
        /// <param name="url">ownCloud instance URL</param>
        /// <param name="user_id">User identifier</param>
        /// <param name="password">Password</param>
        public Client(string url, string user_id, string password) : base(url, user_id, password)
        {
        }
    }
}

