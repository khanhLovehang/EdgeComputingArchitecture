using System.Text.RegularExpressions;

namespace Nodes.Helpers
{
    // Helper class for topic matching (optional)
    public static class MqttTopicMatcher
    {
        public static string ConvertWildcardsToRegex(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Empty;

            // Escape regex special characters except MQTT wildcards
            string escaped = Regex.Escape(pattern)
                                .Replace(@"\+", "([^/]+)")    // Replace MQTT '+' with regex non-slash group
                                .Replace(@"\#", "(.*)");     // Replace MQTT '#' with regex any character group (careful with this at end)

            // Ensure '#' at the end matches correctly or is handled if used mid-topic (invalid MQTT spec usually)
            if (escaped.EndsWith("(.*)"))
            {
                // Allow matching nothing or anything after the final '/' if # is last char
                escaped = escaped.Substring(0, escaped.Length - 4) + @"(/.*)?$";
            }
            else
            {
                escaped += "$"; // Anchor the match to the end of the string
            }

            // Handle $share prefix specifically if present
            if (escaped.StartsWith(@"\$share/"))
            {
                // Match group name, then the rest
                escaped = @"^\$share/([^/]+)/" + escaped.Substring(@"\$share/".Length);
            }
            else
            {
                escaped = "^" + escaped; // Anchor the match to the start of the string
            }

            return escaped;
        }
    }
}
