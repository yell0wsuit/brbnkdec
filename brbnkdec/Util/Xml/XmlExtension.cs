namespace System.Xml
{
    public static class XmlExtension
    {
        public static void WriteSimpleElement(this XmlWriter writer, string elementName, object value)
        {
            writer.WriteStartElement(elementName);
            writer.WriteValue(value);
            writer.WriteEndElement();
        }

        public static void WriteAttributeInt(this XmlWriter writer, string attribute, int i)
        {
            writer.WriteAttributeString(attribute, i.ToString());
        }
    }
}
