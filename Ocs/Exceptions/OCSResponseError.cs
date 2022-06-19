﻿using System;
using System.Diagnostics;
using System.Net;

namespace CompuMaster.Ocs.Exceptions
{
	/// <summary>
	/// OCS API response error
	/// </summary>
	public class OCSResponseError : Exception
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
		/// Initializes a new instance of the <see cref="CompuMaster.Ocs.Exceptions.ResponseError"/> class.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		/// <param name="statusCode">OCS status code associated to the error.</param>
		public OCSResponseError(string message, int ocsStatusCode, string ocsStatusText, HttpStatusCode httpStatusCode) : base(ocsStatusCode + " " + message)
		{
			this.OcsStatusCode = ocsStatusCode;
			this.OcsStatusText = ocsStatusText;
			Debug.WriteLine("ERROR - OCS-StatusCode: " + this.OcsStatusCode.ToString() + "(" + ocsStatusText + ") - HTTP-StatusCode: " + this.HttpStatusCode + " - Message: " + this.Message);
		}
	}
}

