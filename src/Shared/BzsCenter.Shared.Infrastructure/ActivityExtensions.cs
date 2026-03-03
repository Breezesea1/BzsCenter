using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Savvy.Shared.Infrastructure.Extensions;

public static class ActivityExtensions
{
    // Activity Extensions
    extension(Activity? activity)
    {
        // See https://opentelemetry.io/docs/specs/otel/trace/semantic_conventions/exceptions/
        /// <summary>
        /// Adds standard exception-related tags to the current activity based on the provided exception.
        /// </summary>
        /// <remarks>This method sets the exception message, stack trace, and type as tags on the current
        /// activity, and marks the activity status as error according to OpenTelemetry semantic conventions. If there
        /// is no current activity, the method does nothing.</remarks>
        /// <param name="ex">The exception whose details are used to populate the activity's exception tags. Cannot be null.</param>
        public void SetExceptionTags(Exception ex)
        {
            if (activity is null)
            {
                return;
            }

            activity.AddTag("exception.message", ex.Message);
            activity.AddTag("exception.stacktrace", ex.ToString());
            activity.AddTag("exception.type", ex.GetType().FullName);
            activity.SetStatus(ActivityStatusCode.Error);
        }
    }
}