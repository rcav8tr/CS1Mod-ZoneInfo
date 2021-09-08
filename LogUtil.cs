using System;
using System.Reflection;
using UnityEngine;

namespace ZoneInfo
{
    /// <summary>
    /// utility routines for logging
    /// </summary>
    public class LogUtil
    {
        // text to place in front of all messages to make them easier to find in the log file
        private const string MessagePrefix = "[ZoneInfo] ";

        /// <summary>
        /// log an info message
        /// </summary>
        public static void LogInfo(string message)
        {
            Debug.Log(MessagePrefix + message);
        }

        /// <summary>
        /// log an error message with the calling method
        /// </summary>
        public static void LogError(string message)
        {
            // construct the message
            System.Diagnostics.StackTrace stacktrace = new System.Diagnostics.StackTrace();
            System.Diagnostics.StackFrame[] stackFrames = stacktrace.GetFrames();
            if (stackFrames.Length >= 2)
            {
                // prefix message with calling method
                MethodBase mb = stackFrames[1].GetMethod();
                message = MessagePrefix + "Error in " + mb.ReflectedType + "." + mb.Name + ": " + Environment.NewLine + message;
            }
            else
            {
                // just use the prefix alone
                message = MessagePrefix + "Error: " + message;
            }

            // log the message as an error
            Debug.LogError(message);
        }

        /// <summary>
        /// log an exception
        /// </summary>
        public static void LogException(Exception ex)
        {
            // the default exception string includes the message and the stack trace
            Debug.LogError(MessagePrefix + ex.ToString());
        }
    }
}
