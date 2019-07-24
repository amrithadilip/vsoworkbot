// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EmptyBot v4.3.0



namespace VSOWorkBot.Helpers
{
    using System;

    // This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
    // implementation at runtime. Multiple different IBot implementations running at different endpoints can be
    // achieved by specifying a more specific type for the bot constructor argument.
    public static class ArgumentHelpers
    {
        public static void RequireNotNull(this object arg) {
            if (arg == null)
            {
                throw new ArgumentNullException(string.Format("{0} is null.", arg));
            }
        }
    }
}
