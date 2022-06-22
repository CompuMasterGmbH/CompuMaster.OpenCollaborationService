using System;
using System.Diagnostics;
using System.Net;

namespace CompuMaster.Ocs.Exceptions
{
	/// <summary>
	/// OCS API response error
	/// </summary>
	public class OcsResponseException : Exception
	{
		/// <summary>
		/// Gets the OCS status code associated with the error.
		/// </summary>
		/// <value>The status code.</value>
		public int OcsStatusCode { get; }

		/// <summary>
		/// Gets the OCS status code associated with the error.
		/// </summary>
		/// <value>The status code.</value>
		public string OcsStatusText { get; }

		/// <summary>
		/// Gets the HTTP status code associated with the error.
		/// </summary>
		/// <value>The status code.</value>
		public HttpStatusCode HttpStatusCode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">OCS status code associated to the error.</param>
		public OcsResponseException(string message, int ocsStatusCode, string ocsStatusText, HttpStatusCode httpStatusCode) : base(FullMessage(message, ocsStatusCode, ocsStatusText, httpStatusCode))
		{
			this.OcsStatusCode = ocsStatusCode;
			this.OcsStatusText = ocsStatusText;
		}

		/// <summary>
		/// Create the full exception message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="ocsStatusCode"></param>
		/// <param name="ocsStatusText"></param>
		/// <param name="httpStatusCode"></param>
		/// <returns></returns>
		private static string FullMessage(string message, int ocsStatusCode, string ocsStatusText, HttpStatusCode httpStatusCode)
		{
			if (ocsStatusCode != 0 && !String.IsNullOrEmpty(ocsStatusText))
            {
				//OCS error
				if (!String.IsNullOrEmpty(message))
					return "OCS-StatusCode: " + ocsStatusCode.ToString() + " (" + ocsStatusText + "), HTTP-StatusCode: " + ((int)httpStatusCode).ToString() + ", Message: " + message;
				else
					return "OCS-StatusCode: " + ocsStatusCode.ToString() + " (" + ocsStatusText + "), HTTP-StatusCode: " + ((int)httpStatusCode).ToString();
			}
			else if (httpStatusCode != 0)
				//HTTP or network error
				return "HTTP-Error: " + ((int)httpStatusCode).ToString() + " " + message;
			else
				//another unknown error
				return message;
		}
	}
}

