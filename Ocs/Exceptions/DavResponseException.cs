using System;
using System.Diagnostics;
using System.Net;

namespace CompuMaster.Ocs.Exceptions
{
	/// <summary>
	/// Response error
	/// </summary>
	public class DavResponseException : Exception
	{
		/// <summary>
		/// Gets the HTTP status code associated with the error.
		/// </summary>
		/// <value>The status code.</value>
		public int HttpStatusCode { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">HTTP status code associated to the error.</param>
		public DavResponseException(string message, int statusCode, Exception innerException) : base(FullMessage(message, statusCode), innerException)
		{
			this.HttpStatusCode = statusCode;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">HTTP status code associated to the error.</param>
		public DavResponseException(string message, int statusCode) : base(FullMessage(message, statusCode))
		{
			this.HttpStatusCode = statusCode;
		}

		/// <summary>
		/// Create the full exception message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="httpStatusCode"></param>
		/// <returns></returns>
		private static string FullMessage(string message, int httpStatusCode)
		{
			if (httpStatusCode != 0)
				//HTTP or network error
				return "HTTP-Error: " + ((int)httpStatusCode).ToString() + " " + message;
			else
				//another unknown error
				return message;
		}
	}
}

