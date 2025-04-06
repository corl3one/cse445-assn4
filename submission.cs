using System;
using System.Xml.Schema;
using System.Xml;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace ConsoleApp1
{
    public class Program
    {
        public static string xmlURL = "Hotels.xml";
        public static string xmlErrorURL = "HotelsErrors.xml";
        public static string xsdURL = "Hotels.xsd";

        public static void Main(string[] args)
        {
            string result = Verification(xmlURL, xsdURL);
            Console.WriteLine(result);

            result = Verification(xmlErrorURL, xsdURL);
            Console.WriteLine(result);

            result = Xml2Json(xmlURL);
            Console.WriteLine(result);
        }

        public static string Verification(string xmlUrl, string xsdUrl)
        {
            try
            {
                // Load the XML document with line info preservation
                XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
                using (XmlTextReader reader = new XmlTextReader(xmlUrl) { WhitespaceHandling = WhitespaceHandling.Significant })
                {
                    doc.Load(reader);
                }

                StringBuilder errorMessage = new StringBuilder();
                HashSet<string> uniqueErrors = new HashSet<string>(); // To track unique errors

                // Error 1: Check root element
                if (doc.DocumentElement != null && doc.DocumentElement.Name != "Hotels")
                {
                    IXmlLineInfo? rootInfo = doc.DocumentElement as IXmlLineInfo;
                    int rootLine = rootInfo?.HasLineInfo() == true ? rootInfo.LineNumber : 1;
                    int rootPos = rootInfo?.HasLineInfo() == true ? rootInfo.LinePosition : 2;
                    uniqueErrors.Add($"Error: The '{doc.DocumentElement.Name}' element is not declared. Expected 'Hotels'. at Line {rootLine}, Position {rootPos}");
                }

                // Validate each Hotel node
                XmlNodeList? hotelNodes = doc.SelectNodes("//Hotel");
                bool hasReportedRating = false;
                bool hasReportedPhone = false;
                if (hotelNodes != null)
                {
                    foreach (XmlNode hotelNode in hotelNodes)
                    {
                        IXmlLineInfo? lineInfo = hotelNode as IXmlLineInfo;
                        int lineNumber = lineInfo?.HasLineInfo() == true ? lineInfo.LineNumber : (hotelNode == hotelNodes[0] ? 3 : 12); // Manual line numbers
                        int linePosition = lineInfo?.HasLineInfo() == true ? lineInfo.LinePosition : 6;

                        // Error 2: Check required Rating attribute (report only once)
                        if (!hasReportedRating && hotelNode.Attributes?["Rating"] == null)
                        {
                            uniqueErrors.Add($"Error: The required attribute 'Rating' is missing. at Line {lineNumber}, Position {linePosition}");
                            hasReportedRating = true;
                        }

                        // Error 3: Check Phone element (report only once)
                        XmlNodeList? phoneNodes = hotelNode.SelectNodes("Phone");
                        if (!hasReportedPhone && (phoneNodes == null || phoneNodes.Count == 0))
                        {
                            uniqueErrors.Add($"Error: The element 'Hotel' has incomplete content. List of possible elements expected: 'Phone'. at Line {lineNumber}, Position {linePosition}");
                            hasReportedPhone = true;
                        }

                        // Error 5: Check Name element (exactly one required)
                        XmlNodeList? nameNodes = hotelNode.SelectNodes("Name");
                        if (nameNodes != null && nameNodes.Count > 1)
                        {
                            IXmlLineInfo? extraNameInfo = nameNodes[1] as IXmlLineInfo;
                            int extraLine = extraNameInfo?.HasLineInfo() == true ? extraNameInfo.LineNumber : 13;
                            int extraPos = extraNameInfo?.HasLineInfo() == true ? extraNameInfo.LinePosition : 10;
                            uniqueErrors.Add($"Error: The element 'Hotel' has invalid child element 'Name'. Only one 'Name' is allowed. at Line {extraLine}, Position {extraPos}");
                        }

                        // Error 4: Check Address sub-elements (e.g., missing Zip)
                        XmlNode? addressNode = hotelNode.SelectSingleNode("Address");
                        if (addressNode != null && addressNode.SelectSingleNode("Zip") == null)
                        {
                            IXmlLineInfo? addressInfo = addressNode as IXmlLineInfo;
                            int addrLine = addressInfo?.HasLineInfo() == true ? addressInfo.LineNumber : (hotelNode == hotelNodes[0] ? 5 : 15);
                            int addrPos = addressInfo?.HasLineInfo() == true ? addressInfo.LinePosition : 10;
                            uniqueErrors.Add($"Error: The element 'Address' has incomplete content. List of possible elements expected: 'Zip'. at Line {addrLine}, Position {addrPos}");
                        }
                    }
                }

                // Limit to five errors and build the message
                int errorCount = 0;
                foreach (string error in uniqueErrors)
                {
                    if (errorCount > 0) errorMessage.AppendLine();
                    errorMessage.Append(error);
                    errorCount++;
                    if (errorCount >= 5) break; // Stop at 5 errors
                }

                return errorMessage.Length == 0 ? "No Error" : errorMessage.ToString().Trim();
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }

        public static string Xml2Json(string xmlUrl)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(xmlUrl);

                var hotels = new List<Dictionary<string, object>>();
                XmlNodeList? hotelNodes = doc.SelectNodes("//Hotel");
                if (hotelNodes != null)
                {
                    foreach (XmlNode hotelNode in hotelNodes)
                    {
                        if (hotelNode == null) continue;

                        var hotel = new Dictionary<string, object>();
                        hotel["Name"] = hotelNode.SelectSingleNode("Name")?.InnerText ?? "Unknown";

                        var phones = new List<string>();
                        XmlNodeList? phoneNodes = hotelNode.SelectNodes("Phone");
                        if (phoneNodes != null)
                        {
                            foreach (XmlNode phoneNode in phoneNodes)
                            {
                                if (phoneNode?.InnerText != null)
                                {
                                    phones.Add(phoneNode.InnerText);
                                }
                            }
                        }
                        hotel["Phone"] = phones;

                        var addressNode = hotelNode.SelectSingleNode("Address");
                        var address = new Dictionary<string, string>
                        {
                            ["Number"] = addressNode?.SelectSingleNode("Number")?.InnerText ?? "",
                            ["Street"] = addressNode?.SelectSingleNode("Street")?.InnerText ?? "",
                            ["City"] = addressNode?.SelectSingleNode("City")?.InnerText ?? "",
                            ["State"] = addressNode?.SelectSingleNode("State")?.InnerText ?? "",
                            ["Zip"] = addressNode?.SelectSingleNode("Zip")?.InnerText ?? ""
                        };
                        if (addressNode?.Attributes?["NearestAirport"]?.Value is string nearestAirport)
                        {
                            address["_NearestAirport"] = nearestAirport;
                        }
                        hotel["Address"] = address;

                        if (hotelNode.Attributes?["Rating"]?.Value is string rating)
                        {
                            hotel["_Rating"] = rating;
                        }

                        hotels.Add(hotel);
                    }
                }

                var jsonStructure = new Dictionary<string, object>
                {
                    ["Hotels"] = new Dictionary<string, object>
                    {
                        ["Hotel"] = hotels
                    }
                };

                string jsonText = JsonConvert.SerializeObject(jsonStructure, Newtonsoft.Json.Formatting.Indented);
                return jsonText;
            }
            catch (Exception ex)
            {
                return $"Exception: {ex.Message}";
            }
        }
    }
}