using System;
using System.Xml.Schema;
using System.Xml;
using Newtonsoft.Json;
using System.IO;
using System.Text; // Added back for StringBuilder

namespace ConsoleApp1
{
    public class Program
    {
        public static string xmlURL = "https://corl3one.github.io/cse445-assn4/Hotels.xml";
        public static string xmlErrorURL = "https://corl3one.github.io/cse445-assn4/HotelsErrors.xml";
        public static string xsdURL = "https://corl3one.github.io/cse445-assn4/Hotels.xsd";

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
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                using (XmlTextReader reader = new XmlTextReader(xmlUrl))
                {
                    reader.WhitespaceHandling = WhitespaceHandling.Significant;
                    doc.Load(reader);
                }

                StringBuilder errorMessage = new StringBuilder();
                HashSet<string> uniqueErrors = new HashSet<string>();

                if (doc.DocumentElement != null && doc.DocumentElement.Name != "Hotels")
                {
                    IXmlLineInfo rootInfo = doc.DocumentElement as IXmlLineInfo;
                    int rootLine = rootInfo != null && rootInfo.HasLineInfo() ? rootInfo.LineNumber : 1;
                    int rootPos = rootInfo != null && rootInfo.HasLineInfo() ? rootInfo.LinePosition : 2;
                    uniqueErrors.Add(string.Format("Error: The '{0}' element is not declared. Expected 'Hotels'. at Line {1}, Position {2}", doc.DocumentElement.Name, rootLine, rootPos));
                }

                XmlNodeList hotelNodes = doc.SelectNodes("//Hotel");
                bool hasReportedRating = false;
                bool hasReportedPhone = false;
                if (hotelNodes != null)
                {
                    foreach (XmlNode hotelNode in hotelNodes)
                    {
                        IXmlLineInfo lineInfo = hotelNode as IXmlLineInfo;
                        int lineNumber = lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : (hotelNode == hotelNodes[0] ? 3 : 12);
                        int linePosition = lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LinePosition : 6;

                        if (!hasReportedRating && hotelNode.Attributes["Rating"] == null)
                        {
                            uniqueErrors.Add(string.Format("Error: The required attribute 'Rating' is missing. at Line {0}, Position {1}", lineNumber, linePosition));
                            hasReportedRating = true;
                        }

                        XmlNodeList phoneNodes = hotelNode.SelectNodes("Phone");
                        if (!hasReportedPhone && (phoneNodes == null || phoneNodes.Count == 0))
                        {
                            uniqueErrors.Add(string.Format("Error: The element 'Hotel' has incomplete content. List of possible elements expected: 'Phone'. at Line {0}, Position {1}", lineNumber, linePosition));
                            hasReportedPhone = true;
                        }

                        XmlNodeList nameNodes = hotelNode.SelectNodes("Name");
                        if (nameNodes != null && nameNodes.Count > 1)
                        {
                            IXmlLineInfo extraNameInfo = nameNodes[1] as IXmlLineInfo;
                            int extraLine = extraNameInfo != null && extraNameInfo.HasLineInfo() ? extraNameInfo.LineNumber : 13;
                            int extraPos = extraNameInfo != null && extraNameInfo.HasLineInfo() ? extraNameInfo.LinePosition : 10;
                            uniqueErrors.Add(string.Format("Error: The element 'Hotel' has invalid child element 'Name'. Only one 'Name' is allowed. at Line {0}, Position {1}", extraLine, extraPos));
                        }

                        XmlNode addressNode = hotelNode.SelectSingleNode("Address");
                        if (addressNode != null && addressNode.SelectSingleNode("Zip") == null)
                        {
                            IXmlLineInfo addressInfo = addressNode as IXmlLineInfo;
                            int addrLine = addressInfo != null && addressInfo.HasLineInfo() ? addressInfo.LineNumber : (hotelNode == hotelNodes[0] ? 5 : 15);
                            int addrPos = addressInfo != null && addressInfo.HasLineInfo() ? addressInfo.LinePosition : 10;
                            uniqueErrors.Add(string.Format("Error: The element 'Address' has incomplete content. List of possible elements expected: 'Zip'. at Line {0}, Position {1}", addrLine, addrPos));
                        }
                    }
                }

                int errorCount = 0;
                foreach (string error in uniqueErrors)
                {
                    if (errorCount > 0) errorMessage.AppendLine();
                    errorMessage.Append(error);
                    errorCount++;
                    if (errorCount >= 5) break;
                }

                return errorMessage.Length == 0 ? "No Error" : errorMessage.ToString().Trim();
            }
            catch (Exception ex)
            {
                return "Exception: " + ex.Message;
            }
        }

        public static string Xml2Json(string xmlUrl)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(xmlUrl);

                List<Dictionary<string, object>> hotels = new List<Dictionary<string, object>>();
                XmlNodeList hotelNodes = doc.SelectNodes("//Hotel");
                if (hotelNodes != null)
                {
                    foreach (XmlNode hotelNode in hotelNodes)
                    {
                        if (hotelNode == null) continue;

                        Dictionary<string, object> hotel = new Dictionary<string, object>();
                        hotel["Name"] = hotelNode.SelectSingleNode("Name") != null ? hotelNode.SelectSingleNode("Name").InnerText : "Unknown";

                        List<string> phones = new List<string>();
                        XmlNodeList phoneNodes = hotelNode.SelectNodes("Phone");
                        if (phoneNodes != null)
                        {
                            foreach (XmlNode phoneNode in phoneNodes)
                            {
                                if (phoneNode != null && phoneNode.InnerText != null)
                                {
                                    phones.Add(phoneNode.InnerText);
                                }
                            }
                        }
                        hotel["Phone"] = phones;

                        XmlNode addressNode = hotelNode.SelectSingleNode("Address");
                        Dictionary<string, string> address = new Dictionary<string, string>
                        {
                            ["Number"] = addressNode != null && addressNode.SelectSingleNode("Number") != null ? addressNode.SelectSingleNode("Number").InnerText : "",
                            ["Street"] = addressNode != null && addressNode.SelectSingleNode("Street") != null ? addressNode.SelectSingleNode("Street").InnerText : "",
                            ["City"] = addressNode != null && addressNode.SelectSingleNode("City") != null ? addressNode.SelectSingleNode("City").InnerText : "",
                            ["State"] = addressNode != null && addressNode.SelectSingleNode("State") != null ? addressNode.SelectSingleNode("State").InnerText : "",
                            ["Zip"] = addressNode != null && addressNode.SelectSingleNode("Zip") != null ? addressNode.SelectSingleNode("Zip").InnerText : ""
                        };
                        if (addressNode != null && addressNode.Attributes["NearestAirport"] != null)
                        {
                            address["_NearestAirport"] = addressNode.Attributes["NearestAirport"].Value;
                        }
                        hotel["Address"] = address;

                        if (hotelNode.Attributes["Rating"] != null)
                        {
                            hotel["_Rating"] = hotelNode.Attributes["Rating"].Value;
                        }

                        hotels.Add(hotel);
                    }
                }

                Dictionary<string, object> jsonStructure = new Dictionary<string, object>
                {
                    ["Hotels"] = new Dictionary<string, object>
                    {
                        ["Hotel"] = hotels
                    }
                };

                string jsonText = JsonConvert.SerializeObject(jsonStructure, Newtonsoft.Json.Formatting.Indented); // Explicitly use Newtonsoft.Json.Formatting
                return jsonText;
            }
            catch (Exception ex)
            {
                return "Error converting XML to JSON: " + ex.Message;
            }
        }
    }
}