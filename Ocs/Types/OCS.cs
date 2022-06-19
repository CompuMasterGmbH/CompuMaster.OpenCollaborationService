using System;
using System.Xml.Serialization;

namespace CompuMaster.Ocs.Types
{
	/// <summary>
	/// OCS API Response.
	/// </summary>
	[Serializable, XmlRoot("ocs")]
	public class OCS
	{
		/// <summary>
		/// Gets or sets the meta information.
		/// </summary>
		/// <value>The meta.</value>
		[XmlElement("meta")]
		public Meta Meta { get; set; }
		/// <summary>
		/// Gets or sets the data payload.
		/// </summary>
		/// <value>The data.</value>
		[XmlElement("data")]
		public object Data { get; set; }
	}

	/// <summary>
	/// OCS API Meta information.
	/// </summary>
	[Serializable]
	public class Meta {
		/// <summary>
		/// Gets or sets the response status.
		/// </summary>
		/// <value>The status.</value>
		[XmlElement("status")]
		public string Status { get; set; }
		/// <summary>
		/// Gets or sets the response status code.
		/// </summary>
		/// <value>The status code.</value>
		[XmlElement("statuscode")]
		public int StatusCode { get; set; }
		/// <summary>
		/// Gets or sets the response status message.
		/// </summary>
		/// <value>The message.</value>
		[XmlElement("message")]
		public string Message { get; set; }

        public override string ToString()
        {
			return "Status " + this.StatusCode.ToString() + " (" + this.Status + ")" + (!(String.IsNullOrEmpty(this.Message)) ? ", Message: " + this.Message : "");
        }
    }
}

