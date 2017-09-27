﻿using DataModel;
using Dicom.Log;
using Dicom.Network;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PacsOnCloud
{
    class Program
    {
        /// <summary>
        /// Called to adapt the string message before passing through
        /// Adapts messages of the form
        /// Beginning parsing for {@file} using widget {widgetName}
        /// To
        /// Beginning parsing for {0} using widget {1}
        /// Required by loggers that expect positional format rather than named format
        /// such as NLog
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        protected static string NameFormatToPositionalFormat(string message)
        {
            Regex CurlyBracePairRegex = new Regex(@"{.*?}");
            var matches = CurlyBracePairRegex.Matches(message).Cast<Match>();

            var handledMatchNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            //Stores the updated message
            var updatedMessage = message;
            var positionDelta = 0;

            //Is every encountered match a number?  If so, we've been given a string already in positional format so it should not be amended
            var everyMatchIsANumber = true; //until proven otherwise

            foreach (var match in matches)
            {
                //Remove the braces
                var matchNameFormattingNoBraces = match.Value.Substring(1, match.Value.Length - 2);

                //Split into the name and the formatting
                var colonIndex = matchNameFormattingNoBraces.IndexOf(':');
                var matchName = colonIndex < 0
                                    ? matchNameFormattingNoBraces
                                    : matchNameFormattingNoBraces.Substring(0, colonIndex);
                var formattingIncludingColon = colonIndex < 0 ? "" : matchNameFormattingNoBraces.Substring(colonIndex);

                everyMatchIsANumber = everyMatchIsANumber && IsNumber(matchName);

                //Remove leading "@" sign (indicates destructuring was desired)
                var destructured = matchName.StartsWith("@");
                if (destructured)
                {
                    matchName = matchName.Substring(1);
                }

                int ordinalOutputPosition;
                //Already seen the match?
                if (!handledMatchNames.ContainsKey(matchName))
                {
                    //first time
                    ordinalOutputPosition = handledMatchNames.Count;
                    handledMatchNames.Add(matchName, ordinalOutputPosition);
                }
                else
                {
                    //resuse previous number
                    ordinalOutputPosition = handledMatchNames[matchName];
                }

                var replacement = "{" + ordinalOutputPosition + formattingIncludingColon + "}";

                //Substitute the new text in place in the message
                updatedMessage = updatedMessage.Substring(0, match.Index + positionDelta) + replacement
                                 + updatedMessage.Substring(match.Index + match.Length + positionDelta);
                //Update positionDelta to account for differing lengths of substitution
                positionDelta = positionDelta + (replacement.Length - match.Length);
            }


            if (everyMatchIsANumber)
            {
                return message;
            }

            return updatedMessage;
        }

        /// <summary>
        /// Checks whether string represents an integer value.
        /// </summary>
        /// <param name="s">String potentially containing integer value.</param>
        /// <returns>True if <paramref name="s"/> could be interpreted as integer value, false otherwise.</returns>
        internal static bool IsNumber(string s)
        {
            int dummy;
            return int.TryParse(s, out dummy);
        }


        static void Main(string[] args)
        {
            MyDicomServer server;
            Console.WriteLine("This is server running...");
            try
            {
                int port = 12345;
                LogManager.SetImplementation(new MinLogManager("127.0.0.1"));
                Logger logger = LogManager.GetLogger("a");

                server = new MyDicomServer(port, logger);
                server.Run();

                string msg = "Searching {path}\\{wildcard} for Dicom codecs";
                string path = "A";
                string wildcard = "B";
                string s = string.Format(NameFormatToPositionalFormat(msg), path, wildcard);
                Console.WriteLine(s);

                string name = Process.GetCurrentProcess().ProcessName;
                logger.Debug($"Process name: {name}");

                string hostname = Dns.GetHostName();
                logger.Debug("Hostname: {hostname}", hostname);

                IPHostEntry localhost = Dns.GetHostEntry(hostname);
                IPAddress[] address = localhost.AddressList;
                IPAddress theOne = address.Where(t => t.AddressFamily == AddressFamily.InterNetwork).First();
                logger.Debug(theOne.ToString());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
