using System;

namespace Misho.Cloud.MegaNz
{
    public class ApiRequestFailedEventArgs : EventArgs
    {
        public ApiRequestFailedEventArgs(Uri url, int attemptNum, int delayMilliseconds, ApiResultCode apiResult, string responseJson)
          : this(url, attemptNum, delayMilliseconds, apiResult, responseJson, null)
        {
        }

        public ApiRequestFailedEventArgs(Uri url, int attemptNum, int delayMilliseconds, ApiResultCode apiResult, Exception exception)
          : this(url, attemptNum, delayMilliseconds, apiResult, null, exception)
        {
        }

        private ApiRequestFailedEventArgs(Uri url, int attemptNum, int delayMilliseconds, ApiResultCode apiResult, string responseJson, Exception exception)
        {
            ApiUrl = url;
            AttemptNum = attemptNum;
            DelayMilliseconds = delayMilliseconds;
            ApiResult = apiResult;
            ResponseJson = responseJson;
            Exception = exception;
        }

        public Uri ApiUrl { get; private set; }

        public ApiResultCode ApiResult { get; private set; }

        public string ResponseJson { get; private set; }

        public int AttemptNum { get; private set; }

        public int DelayMilliseconds { get; private set; }

        public Exception Exception { get; private set; }
    }
}