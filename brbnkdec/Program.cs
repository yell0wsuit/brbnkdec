using System.Xml;

namespace brbnkdec
{
    class Program
    {
        static int lastKeyRange = 0;
        static int lastVelRange = 0;
        static string outputName = "";
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("brbnkdec [file.brbnk] [output.rbnk]");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine("File not found: " + args[0]);
                return;
            }

            outputName = Path.GetFileNameWithoutExtension(args[1]);
            decode(args[0], args[1]);
        }

        static void decode(string inputPath, string outputPath)
        {
            EndianReader reader = new EndianReader(File.Open(inputPath, FileMode.Open), Endianness.BigEndian);
            string fileMagic = reader.ReadString(4);
            if (fileMagic != "RBNK")
            {
                Console.WriteLine("Invalid File Magic: " + fileMagic);
                return;
            }

            ushort fileBOM = reader.ReadUInt16();
            if (fileBOM != 0xFEFF)
            {
                Console.WriteLine("Invalid Byte Order Mark: 0x" + fileBOM.ToString("X4"));
                return;
            }

            byte versionMajor = reader.ReadByte(); // 1
            byte versionMinor = reader.ReadByte(); // 2
            int fileLength = reader.ReadInt32();
            ushort headerLength = reader.ReadUInt16();
            ushort sectionCount = reader.ReadUInt16();
            bool dataSectionFound = false;
            for (int i = 0; i < sectionCount; i++)
            {
                int sectionOffset = reader.ReadInt32();
                int sectionLength = reader.ReadInt32();
                reader.Position = sectionOffset;
                string sectionMagic = reader.ReadString(4);
                if (sectionMagic == "DATA")
                {
                    dataSectionFound = true;
                    break;
                }
            }

            if (!dataSectionFound)
            {
                Console.WriteLine("DATA section not found");
                return;
            }

            // Begin writing the XML
            XmlWriterSettings xmlWriterSettings = new XmlWriterSettings() { Indent = true };
            XmlWriter writer = XmlWriter.Create(outputPath, xmlWriterSettings);
            writer.WriteStartDocument();
            writer.WriteStartElement("nintendoware_snd");
            writer.WriteAttributeString("version", "1.1.0");
            writer.WriteAttributeString("platform", "Revolution");
            writer.WriteStartElement("head");
            writer.WriteStartElement("create");
            writer.WriteAttributeString("user", Environment.UserName);
            writer.WriteAttributeString("host", Environment.MachineName);
            writer.WriteAttributeString("date", DateTime.Now.ToString("s"));
            writer.WriteEndElement(); // create
            writer.WriteSimpleElement("title", "NintendoWare Bank");
            writer.WriteStartElement("generator");
            writer.WriteAttributeString("name", "brbnkdec");
            writer.WriteAttributeString("version", "1, 0, 0, 0");
            writer.WriteEndElement(); // generator
            writer.WriteEndElement(); // head
            writer.WriteStartElement("body");
            writer.WriteStartElement("bank");

            int dataSectionLength = reader.ReadInt32();
            int instrumentDataTablePosition = (int)reader.Position;
            int instrumentCount = reader.ReadInt32();
            writer.WriteStartElement("inst_array");
            writer.WriteAttributeInt("size", instrumentCount);

            for (int i = 0; i < instrumentCount; i++)
            {
                writer.WriteStartElement("inst");
                writer.WriteAttributeString("name", "INST_" + i);
                writer.WriteAttributeInt("prg_no", i);
                writer.WriteAttributeString("adsr_envelope_select", "VelRegion");
                writer.WriteSimpleElement("volume", 1f);
                writer.WriteSimpleElement("fine_tune", 0);
                writer.WriteSimpleElement("coarse_tune", 0);
                writeAsdrEnvelope(writer, 127, 127, 127, 127, 127);

                if (!readInstrumentElement(reader, writer, instrumentDataTablePosition, true))
                    return;

                writer.WriteEndElement(); // inst
                lastKeyRange = 0;
                lastVelRange = 0;
            }

            writer.WriteEndElement(); // inst_array
            writer.WriteEndElement(); // bank
            writer.WriteEndElement(); // body
            writer.WriteEndElement(); // nintendoware_snd
            reader.Close();
            writer.Close();
        }

        static void writeAsdrEnvelope(XmlWriter writer, sbyte attack, sbyte hold, sbyte decay, sbyte sustain, sbyte release)
        {
            writer.WriteStartElement("adsr_envelope");
            writer.WriteSimpleElement("attack", attack);
            writer.WriteSimpleElement("hold", hold);
            writer.WriteSimpleElement("decay", decay);
            writer.WriteSimpleElement("sustain", sustain);
            writer.WriteSimpleElement("release", release);
            writer.WriteEndElement();
        }

        static bool readInstrumentElement(EndianReader reader, XmlWriter writer, int instrumentDataTablePosition, bool isKeyRegion)
        {
            byte instrumentParameterMagic = reader.ReadByte();
            if (instrumentParameterMagic != 1)
            {
                Console.WriteLine("Unknown Instrument Parameter magic: " + instrumentParameterMagic + " at 0x" + (reader.Position - 1).ToString("X"));
                // return false;
            }

            writer.WriteStartElement(isKeyRegion ? "key_region_array" : "vel_region_array");
            int instrumentParameterTypePosition = (int)reader.Position;
            byte instrumentParameterType = reader.ReadByte();
            reader.Position += 2;
            int instrumentParameterOffset = reader.ReadInt32();
            int instrumentParamenterEndPosition = (int)reader.Position;
            reader.Position = instrumentDataTablePosition + instrumentParameterOffset;
            switch (instrumentParameterType)
            {
                case 1:
                    writer.WriteAttributeInt("size", 1);
                    writer.WriteStartElement(isKeyRegion ? "key_region" : "vel_region");
                    writer.WriteAttributeInt("range_min", isKeyRegion ? lastKeyRange : lastVelRange);
                    writer.WriteAttributeInt("range_max", 127);
                    if (isKeyRegion)
                    {
                        writer.WriteSimpleElement("pan", 64);
                        writer.WriteStartElement("vel_region_array");
                        writer.WriteAttributeInt("size", 1);
                        writer.WriteStartElement("vel_region");
                        writer.WriteAttributeInt("range_min", lastVelRange);
                        writer.WriteAttributeInt("range_max", 127);
                    }
                    int audioIndex = reader.ReadInt32();
                    writer.WriteStartElement("file");
                    writer.WriteAttributeString("path", "../inst/" + outputName + "/" + audioIndex + ".wav");
                    writer.WriteAttributeString("encode", "Adpcm");
                    writer.WriteEndElement();

                    sbyte attack = reader.ReadSByte();
                    sbyte decay = reader.ReadSByte();
                    sbyte sustain = reader.ReadSByte();
                    sbyte release = reader.ReadSByte();
                    sbyte hold = reader.ReadSByte();
                    byte waveDataLocationType = reader.ReadByte();
                    byte noteOffType = reader.ReadByte();
                    byte alternateAssign = reader.ReadByte();
                    byte originalKey = reader.ReadByte();
                    byte volume = reader.ReadByte();
                    if (volume > 127)
                        volume = 127;
                    byte pan = reader.ReadByte();
                    if (pan > 127)
                        pan = 127;
                    byte surroundPan = reader.ReadByte();
                    if (surroundPan > 127)
                        surroundPan = 127;
                    float pitch = reader.ReadFloat();
                    int fullCents = (int)Math.Round(1200 * Math.Log2(pitch));
                    int semitones = fullCents / 100;
                    int cents = fullCents % 100;

                    writeAsdrEnvelope(writer, attack, hold, decay, sustain, release);
                    writer.WriteSimpleElement("pan", pan);
                    writer.WriteSimpleElement("volume", (float)(volume / 127));
                    writer.WriteSimpleElement("fine_tune", cents);
                    writer.WriteSimpleElement("coarse_tune", semitones);
                    writer.WriteSimpleElement("original_key", originalKey);
                    writer.WriteStartElement("note_off");
                    writer.WriteAttributeString("type", noteOffType == 0 ? "release" : "ignore");
                    writer.WriteEndElement(); // note_off
                    writer.WriteSimpleElement("alternate_assign", alternateAssign);
                    writer.WriteEndElement(); // vel_region
                    writer.WriteEndElement(); // vel_region_array
                    if (isKeyRegion)
                    {
                        writer.WriteEndElement(); // key_region
                        writer.WriteEndElement(); // key_region_array
                    }
                    break;

                case 2:
                    {
                        byte regionCount = reader.ReadByte();
                        writer.WriteAttributeInt("size", regionCount);
                        List<byte> regionLastIds = new List<byte>();
                        for (int i = 0; i < regionCount; i++)
                        {
                            regionLastIds.Add(reader.ReadByte());
                        }
                        reader.AlignPosition(4);

                        if (isKeyRegion)
                        {
                            for (int i = 0; i < regionCount; i++)
                            {
                                byte nextInstrumentParameterMagic = reader.ReadByte();
                                byte nextInstrumentParameterType = reader.ReadByte();
                                if (nextInstrumentParameterMagic != 1 || (nextInstrumentParameterType != 1 && nextInstrumentParameterType != 2))
                                    continue;

                                reader.Position -= 2;
                                writer.WriteStartElement("key_region");
                                writer.WriteAttributeInt("range_min", i == 0 ? 0 : regionLastIds[i - 1] + 1);
                                writer.WriteAttributeInt("range_max", regionLastIds[i]);
                                writer.WriteSimpleElement("pan", 64);
                                if (!readInstrumentElement(reader, writer, instrumentDataTablePosition, false))
                                    return false;
                                writer.WriteEndElement(); // key_region
                            }
                            writer.WriteEndElement(); // key_region_array
                        }
                        else
                        {
                            Console.WriteLine("Multiple velocity ranges are not supported yet :(");
                            return false;
                        }
                    }
                    break;

                case 3:
                    {
                        byte minimumIndex = reader.ReadByte();
                        byte maximumIndex = reader.ReadByte();
                        int indexCount = maximumIndex - minimumIndex + 1;
                        writer.WriteAttributeInt("size", indexCount);
                        reader.AlignPosition(4);
                        if (isKeyRegion)
                        {
                            for (int i = minimumIndex; i <= maximumIndex; i++)
                            {
                                byte nextInstrumentParameterMagic = reader.ReadByte();
                                byte nextInstrumentParameterType = reader.ReadByte();
                                if (nextInstrumentParameterMagic != 1 || (nextInstrumentParameterType != 1 && nextInstrumentParameterType != 2))
                                    continue;

                                reader.Position -= 2;
                                writer.WriteStartElement("key_region");
                                writer.WriteAttributeInt("range_min", i);
                                writer.WriteAttributeInt("range_max", i);
                                writer.WriteSimpleElement("pan", 64);
                                if (!readInstrumentElement(reader, writer, instrumentDataTablePosition, false))
                                    return false;
                                writer.WriteEndElement(); // key_region
                            }
                            writer.WriteEndElement(); // key_region_array
                        }
                        else
                        {
                            Console.WriteLine("Multiple velocity ranges are not supported yet :(");
                            return false;
                        }
                    }
                    break;

                case 4:
                    break;

                default:
                    Console.WriteLine("Unknown Instrument Parameter type: " + instrumentParameterType + " at 0x" + instrumentParameterTypePosition.ToString("X"));
                    return false;
            }
            reader.Position = instrumentParamenterEndPosition;
            return true;
        }
    }
}
