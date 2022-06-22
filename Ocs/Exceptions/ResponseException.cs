using System;
using System.Diagnostics;
using System.Net;

namespace CompuMaster.Ocs.Exceptions
{
	/// <summary>
	/// Response error
	/// </summary>
	public class ResponseException : Exception
	{
		/// <summary>
		/// Gets the HTTP status code associated with the error.
		/// </summary>
		/// <value>The status code.</value>
		public HttpStatusCode HttpStatusCode { get; }

		/// <summary>
		/// The content of the response containing meta data as XML
		/// </summary>
		public string ResponseContent { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">HTTP status code associated to the error.</param>
		public ResponseException(string message, HttpStatusCode statusCode, Exception innerException, string responseContent) : base(FullMessage(message, statusCode), innerException) 
		{
			this.HttpStatusCode = statusCode;
			this.ResponseContent = responseContent;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseException"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">HTTP status code associated to the error.</param>
		public ResponseException (string message, HttpStatusCode statusCode, string responseContent) : base(FullMessage(message, statusCode)) {
			this.HttpStatusCode = statusCode;
			this.ResponseContent = responseContent;
		}

		/// <summary>
		/// Create the full exception message
		/// </summary>
		/// <param name="message"></param>
		/// <param name="httpStatusCode"></param>
		/// <returns></returns>
		private static string FullMessage(string message, HttpStatusCode httpStatusCode)
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

