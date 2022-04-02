namespace Loupedeck.SpeedtestPlugin.Classes
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;

    using Newtonsoft.Json;

    internal static class BasicLog
    {
        public static Boolean DebugLogging = true;
        private class TraceLogObj
        {

            public Object obj;

            public String message;

            public Int32 source_line_number;

            public String member_name;

            public String source_file_path;

            public DateTime stamp;
            public override String ToString()
            {
                //var shortPath = ApiLogger.GetFileNameFromFullPath(source_file_path);
                var msg = $"{this.stamp:mm:ss.ff} {this.source_file_path}:{this.source_line_number}::{this.member_name}: {this.message}";
                if (this.obj != null)
                {
                    String serialized;
                    try
                    {
                        serialized = JsonConvert.SerializeObject(this.obj, Formatting.Indented);
                    }
                    catch
                    {
                        serialized = $"Serialization error of type: {this.obj?.GetType()}";
                    }
                    msg += " ## " + serialized.Replace("\n", "\n\t").Trim();
                }

                return msg;
            }
        }

        [Conditional("DEBUG")]
        public static void LogEvt(Object obj, String message, [CallerMemberName] String member_name = "", [CallerFilePath] String source_file_path = "", [CallerLineNumber] Int32 source_line_number = 0, [CallerArgumentExpression("obj")] String objVarName = null)
        {
            if (!DebugLogging)
                return;
            var itm = new TraceLogObj { obj = obj, member_name = member_name, message = message, source_file_path = source_file_path, source_line_number = source_line_number, stamp = DateTime.Now };
            Debug.WriteLine(itm);
        }
        [Conditional("DEBUG")]
        public static void LogEvt(String message, [CallerMemberName] String member_name = "", [CallerFilePath] String source_file_path = "", [CallerLineNumber] Int32 source_line_number = 0) =>
            LogEvt(null, message, member_name, source_file_path, source_line_number);

    }
}
#if !NET5_0 && !NET6_0
namespace System.Runtime.CompilerServices
{

    /// <summary>
    /// Allows capturing of the expressions passed to a method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Runtime.CompilerServices.CallerArgumentExpressionAttribute" /> class.
        /// </summary>
        /// <param name="parameterName">The name of the targeted parameter.</param>
        public CallerArgumentExpressionAttribute(String parameterName) => this.ParameterName = parameterName;

        /// <summary>
        /// Gets the target parameter name of the <c>CallerArgumentExpression</c>.
        /// </summary>
        /// <returns>
        /// The name of the targeted parameter of the <c>CallerArgumentExpression</c>.
        /// </returns>
        public String ParameterName { get; }
    }
}
#endif