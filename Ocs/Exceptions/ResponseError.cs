using System;
using System.Diagnostics;
using System.Net;

namespace CompuMaster.Ocs.Exceptions
{
	/// <summary>
	/// Response error
	/// </summary>
	public class ResponseError : Exception
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
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseError"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">HTTP status code associated to the error.</param>
		public ResponseError(string message, HttpStatusCode statusCode, Exception innerException, string responseContent) : base(message, innerException) 
		{
			this.HttpStatusCode = statusCode;
			this.ResponseContent = responseContent;
			Debug.WriteLine("ERROR - HTTP-StatusCode: " + this.HttpStatusCode + " - Message: " + this.Message);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseError"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">HTTP status code associated to the error.</param>
		public ResponseError (string message, HttpStatusCode statusCode, string responseContent) : base(message) {
			this.HttpStatusCode = statusCode;
			this.ResponseContent = responseContent;
			Debug.WriteLine ("ERROR - HTTP-StatusCode: " + this.HttpStatusCode + " - Message: " + this.Message);
		}
	}
}

