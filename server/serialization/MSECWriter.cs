﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps.Serialization
{
    public class MSECSerializer : SectorMetadataSerializer
    {
        public MSECSerializer()
        {
        }

        public override void Serialize(TextWriter writer, Sector sector)
        {
            var serializer = new Serializer(sector, writer);
            serializer.Serialize();
            writer.Flush();
        }

        private class Serializer
        {
            private Sector sector;
            private TextWriter writer;

            public Serializer(Sector sector, TextWriter writer)
            {
                this.sector = sector;
                this.writer = writer;
            }

            public void Serialize()
            {
                // Header
                //
                writer.WriteLine("# Generated by http://www.travellermap.com");
                writer.WriteLine("# " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", DateTimeFormatInfo.InvariantInfo));
                writer.WriteLine();

                // Sector name
                //
                writer.WriteLine("sector " + sector.Names[0].Text);

                if (!String.IsNullOrEmpty(sector.Domain))
                    writer.WriteLine("domain " + sector.Domain);

                if (!String.IsNullOrEmpty(sector.AlphaQuadrant))
                    writer.WriteLine("alpha " + sector.AlphaQuadrant);
                if (!String.IsNullOrEmpty(sector.BetaQuadrant))
                    writer.WriteLine("beta " + sector.BetaQuadrant);
                if (!String.IsNullOrEmpty(sector.GammaQuadrant))
                    writer.WriteLine("gamma " + sector.GammaQuadrant);
                if (!String.IsNullOrEmpty(sector.DeltaQuadrant))
                    writer.WriteLine("delta " + sector.DeltaQuadrant);

                writer.WriteLine();

                // Subsectors
                //
                if (sector.Subsectors.Count > 0)
                {
                    writer.WriteLine("# Subsectors");
                    writer.WriteLine("#");
                    for (int i = 0; i < 16; i++)
                    {
                        Subsector ss = sector[i];
                        if (ss != null)
                            writer.WriteLine("" + (char)('a' + i) + " " + ss.Name);
                        else
                            writer.WriteLine("" + (char)('a' + i));
                    }
                    writer.WriteLine();
                }

                // Borders, Routes and Labels - group by allegiance
                //
                List<IAllegiance> list = new List<IAllegiance>();
                list.AddRange(sector.Allegiances); // TODO: Output stock allegiances
                list.AddRange(sector.Borders);
                list.AddRange(sector.Routes);
                list.AddRange(sector.Labels);

                // Output grouped by allegiance
                //
                list.Sort(CompareAllegiances);
                bool isFirst = true;
                string code = null;
                Allegiance alleg = null;
                foreach (IAllegiance item in list)
                {
                    // Determine allegiance
                    if (isFirst || item.Allegiance != code)
                    {
                        isFirst = false;
                        code = item.Allegiance;
                        alleg = null;

                        if (code != null)
                            alleg = sector.GetAllegianceFromCode(code);

                        if (alleg != null)
                        {
                            writer.WriteLine();
                            writer.Write("# ");
                            writer.WriteLine(alleg.Name);
                            writer.WriteLine("#");
                        }
                        else
                        {
                            writer.WriteLine();
                            writer.WriteLine("# Other");
                            writer.WriteLine("#");
                        }
                    }

                    // Output the item
                    if (item is Allegiance)
                        WriteAllegiance(item as Allegiance);
                    else if (item is Border)
                        WriteBorder(item as Border, alleg, sector);
                    else if (item is Label)
                        WriteLabel(item as Label);
                    else if (item is Route)
                        WriteRoute(item as Route, sector);
                }
            }

            private void WriteAllegiance(Allegiance allegiance)
            {
                writer.Write("ally ");
                writer.Write(allegiance.T5Code);
                writer.Write(" ");
                writer.Write(allegiance.Name);
                writer.WriteLine();
            }

            private void WriteRoute(Route route, Sector sector)
            {
                writer.Write("route ");

                if (route.StartOffsetX != 0 || route.StartOffsetY != 0)
                {
                    writer.Write(route.StartOffsetX.ToString(CultureInfo.InvariantCulture));
                    writer.Write(" ");
                    writer.Write(route.StartOffsetY.ToString(CultureInfo.InvariantCulture));
                    writer.Write(" ");
                }
                writer.Write(route.Start.ToString("0000", CultureInfo.InvariantCulture));

                writer.Write(" ");

                if (route.EndOffsetX != 0 || route.EndOffsetY != 0)
                {
                    writer.Write(route.EndOffsetX.ToString(CultureInfo.InvariantCulture));
                    writer.Write(" ");
                    writer.Write(route.EndOffsetY.ToString(CultureInfo.InvariantCulture));
                    writer.Write(" ");
                }
                writer.Write(route.End.ToString("0000", CultureInfo.InvariantCulture));

                SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("route", route.Allegiance);
                Color? color = route.Color ?? ssr.GetColor("color");
                if (color.HasValue)
                {
                    writer.Write(" ");
                    writer.Write(ColorTranslator.ToHtml(color.Value).ToLowerInvariant());
                }

                writer.WriteLine();
            }

            private void WriteLabel(Label label)
            {
                string[] lines = label.Text.Split('\n');

                const int lineOffset = -60;
                int offset = -(lineOffset * (lines.Length - 1)) / 2;

                foreach (string line in lines)
                {
                    if (line.Length > 0)
                    {
                        writer.Write("label ");
                        writer.Write(label.Hex.ToString("0000", CultureInfo.InvariantCulture));
                        if (offset != 0)
                        {
                            writer.Write("/");
                            writer.Write(offset.ToString("+0;-0", CultureInfo.InvariantCulture));
                        }
                        writer.Write(" ");
                        writer.Write(line);
                        writer.WriteLine();
                    }

                    offset += lineOffset;
                }

                // TODO: Other properties
            }

            private void WriteBorder(Border border, Allegiance alleg, Sector sector)
            {
                if (border.ShowLabel && (border.Label != null || alleg != null))
                {
                    writer.Write("label ");
                    writer.Write(border.LabelPositionHex.ToString("0000", CultureInfo.InvariantCulture));
                    writer.Write(" ");
                    writer.Write(border.Label ?? alleg.Name);
                    writer.WriteLine();
                }

                writer.Write("border ");
                writer.Write(border.PathString);

                SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("border", alleg.T5Code);
                Color? color = border.Color ?? ssr.GetColor("color");
                if (color.HasValue)
                {
                    writer.Write(" ");
                    writer.Write(ColorTranslator.ToHtml(color.Value).ToLowerInvariant());
                }
                writer.WriteLine();
            }

            private static int CompareAllegiances(IAllegiance a, IAllegiance b)
            {
                if (a == null)
                    return b == null ? 0 : -1;

                if (b == null)
                    return 1;
                    
                if (string.Equals(a.Allegiance, b.Allegiance))
                    return string.Compare(a.GetType().ToString(), b.GetType().ToString());

                return string.Compare(a.Allegiance, b.Allegiance);
            }
        }
    }
}