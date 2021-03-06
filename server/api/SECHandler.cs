﻿using System.IO;
using System.Net.Mime;
using System.Text;
using System.Web;

namespace Maps.API
{
    internal class SECHandler : DataHandlerBase
    {
        protected override string ServiceName { get { return "sec"; } }
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }

            public override void Process()
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                ResourceManager resourceManager = new ResourceManager(context.Server);
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));
                Sector sector;

                bool sscoords = GetBoolOption("sscoords", defaultValue: false);
                bool includeMetadata = GetBoolOption("metadata", defaultValue: true);
                bool includeHeader = GetBoolOption("header", defaultValue: true);

                if (context.Request.HttpMethod == "POST")
                {
                    bool lint = GetBoolOption("lint", defaultValue: false);
                    var errors = lint ? new ErrorLogger() : null;
                    sector = new Sector(context.Request.InputStream, new ContentType(context.Request.ContentType).MediaType, errors);
                    if (lint && !errors.Empty)
                        throw new HttpError(400, "Bad Request", errors.ToString());
                    includeMetadata = false;
                }
                else if (HasOption("sx") && HasOption("sy"))
                {
                    int sx = GetIntOption("sx", 0);
                    int sy = GetIntOption("sy", 0);

                    sector = map.FromLocation(sx, sy);

                    if (sector == null)
                        throw new HttpError(404, "Not Found", string.Format("The sector at {0},{1} was not found.", sx, sy));
                }
                else if (HasOption("sector"))
                {
                    string sectorName = GetStringOption("sector");
                    sector = map.FromName(sectorName);  

                    if (sector == null)
                        throw new HttpError(404, "Not Found", string.Format("The specified sector '{0}' was not found.", sectorName));
                }
                else
                {
                    throw new HttpError(400, "Bad Request", "No sector specified.");
                }

                WorldFilter filter = null;
                if (HasOption("subsector"))
                {
                    string subsector = GetStringOption("subsector");
                    int index = sector.SubsectorIndexFor(subsector);
                    if (index == -1)
                        throw new HttpError(404, "Not Found", string.Format("The specified subsector '{0}' was not found.", subsector));
                    filter = (World world) => (world.Subsector == index);
                }
                else if (HasOption("quadrant"))
                {
                    string quadrant = GetStringOption("quadrant");
                    int index = Sector.QuadrantIndexFor(quadrant);
                    if (index == -1)
                        throw new HttpError(400, "Bad Request", string.Format("The specified quadrant '{0}' is invalid.", quadrant));
                    filter = (World world) => (world.Quadrant == index);
                }

                string mediaType = GetStringOption("type");
                Encoding encoding;
                switch (mediaType)
                {
                    case "SecondSurvey":
                    case "TabDelimited":
                        encoding = Util.UTF8_NO_BOM;
                        break;
                    default:
                        encoding = Encoding.GetEncoding(1252);
                        break;
                }

                string data;
                using (var writer = new StringWriter())
                {
                    // Content
                    //
                    sector.Serialize(resourceManager, writer, mediaType, includeMetadata: includeMetadata, includeHeader: includeHeader, sscoords: sscoords, filter: filter);
                    data = writer.ToString();
                }
                SendResult(context, data, encoding);
            }
        }
    }
}