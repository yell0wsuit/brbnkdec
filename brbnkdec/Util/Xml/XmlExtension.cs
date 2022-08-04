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
    }
}
