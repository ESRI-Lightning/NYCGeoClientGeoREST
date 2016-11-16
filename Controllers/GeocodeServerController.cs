using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Web.Http;
using System.Configuration;
using System.Dynamic;
using Newtonsoft.Json;

namespace GeoREST.Controllers
{
    public class GeocodeServerController : ApiController
    {
        private QueryParams queryParams;
        private string GeoClientAPIURL = "https://api.cityofnewyork.us/geoclient/v1/";
        //private bool nullResult = false;
        //private string format = "html";

        public GeocodeServerController()
        {
            this.queryParams = new QueryParams();
        }

        // GET api/GeocodeServer
        public HttpResponseMessage Get()
        {

            //check for format string
            var query = this.Request.GetQueryNameValuePairs();

            #region parse format
            var matches = query.Where(kv => kv.Key.ToLower() == "f");

            string sFormat = "html";
            if (matches.Count() > 0)
            {
                sFormat = matches.First().Value.ToLower();
            }
            #endregion

            string sCallback = null;
            #region parse callback
            var matches2 = query.Where(kv => kv.Key.ToLower().IndexOf("callback") > -1);
            if (matches2.Count() > 0)
            {
                sCallback = matches2.First().Value;
            }
            #endregion

            if (sFormat == "html")
            {
                string result = this.getHTMLPageGeocodeServer();
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new StringContent(result, System.Text.Encoding.UTF8, "text/html");
                return resp;
            }
            else
            {
                string result = this.getJSONPageGeocodeServer();
                if (sCallback != null) result = sCallback + "(" + result + ");";
                var resp = new HttpResponseMessage(HttpStatusCode.OK);
                resp.Content = new StringContent(result, System.Text.Encoding.UTF8, "text/json");
                return resp;
            }

        }

        private string getJSONPageGeocodeServer()
        {
            MemoryStream mstream = new MemoryStream();
            DataContractJsonSerializer ser2 = new DataContractJsonSerializer(typeof(GeocodeServerMetadata));
            ser2.WriteObject(mstream, new GeocodeServerMetadata());

            mstream.Position = 0;
            StreamReader sr = new StreamReader(mstream);
            string result = sr.ReadToEnd();

            return result;
        }

        private string getHTMLPageGeocodeServer()
        {
            return "<a href='GeocodeServer/findAddressCandidates'>Find Address Candidates</a><br/>" +
                   "<a href='GeocodeServer/find'>Search</a>";
        }

        [HttpGet]
        [ActionName("findAddressCandidates")]
        public HttpResponseMessage execute()
        {
            return findCandidates("findAddressCandidates");
        }

        [HttpGet]
        [ActionName("find")]
        public HttpResponseMessage find()
        {
            return findCandidates("find");
        }

        #region Do Geocoding
        public HttpResponseMessage findCandidates(string action)
        {
            string errMsg = "";
          
            var query = this.Request.GetQueryNameValuePairs();
            var q = this.Request.RequestUri;

            if (query.Count() == 0)
                errMsg = "{error: {status:\"rejected\", message: \"Your input query parameters are invalid\"}}";

            if (query.Count() > 0)
            {
                #region Parse response format
                //var matchesF = query.Where(kv => kv.Key.ToLower() == "f");

                //this.format = "html";
                //if (matchesF.Count() > 0)
                //{
                //    this.format = matchesF.First().Value.ToLower();
                //    if (this.format.ToLower() != "html")
                //        errMsg = errMsg = "{error: {status:\"rejected\", message: \"Invalid response format\"}}";
                //}
                #endregion

                #region Parse search parameters

                Dictionary<string, string> d = query.toUpperKeyDictionary();

                // Add MANHATTAN as default borough
                if (!d.ContainsKey("BOROUGH") || string.IsNullOrEmpty(d["BOROUGH"])) d.Add("BOROUGH", "Manhattan");
                this.queryParams.searchURL = "";

                if (d.ContainsKey("NAME"))
                {
                    this.queryParams.searchObject = new PlaceSearch(d);
                    this.queryParams.searchFile = "place.json";
                }
                else if (d.ContainsKey("HOUSENUMBER"))
                {
                    this.queryParams.searchObject = new AddressSearch(d);
                    this.queryParams.searchFile = "address.json";
                }
                else if (d.ContainsKey("LOT"))
                {
                    this.queryParams.searchObject = new BBLSearch(d);
                    this.queryParams.searchFile = "bbl.json";
                }
                else if (d.ContainsKey("BIN"))
                {
                    this.queryParams.searchObject = new BINSearch(d);
                    this.queryParams.searchFile = "bin.json";
                }
                else if (d.ContainsKey("ONSTREET"))
                {
                    this.queryParams.searchObject = new BlockFaceSearch(d);
                    this.queryParams.searchFile = "blockface.json";
                }
                else if (d.ContainsKey("CROSSSTREETTWO") && !d.ContainsKey("ONSTREET"))
                {
                    this.queryParams.searchObject = new IntersectionSearch(d);
                    this.queryParams.searchFile = "intersection.json";
                }
                else if (d.ContainsKey("SINGLELINE"))
                {
                    this.queryParams.searchObject = new SingleInputSearch(d);
                    this.queryParams.searchFile = "search.json";
                }
                else if (d.ContainsKey("TEXT"))
                {
                    this.queryParams.searchObject = new SingleInputSearch(d);
                    this.queryParams.searchFile = "search.json";
                }
                else
                {
                    this.queryParams._rawQuery = this.Request.RequestUri.Query.Remove(0, 1).Replace("\n", " ");
                    this.queryParams.searchObject = new SingleInputSearch(d);
                    this.queryParams.searchFile = "search.json";
                    this.queryParams.searchURL = String.Format(this.GeoClientAPIURL + "{0}?input={1}", this.queryParams.searchFile, this.queryParams._rawQuery);
                }

                if (this.queryParams.searchURL == "")
                {
                    this.queryParams.searchURL = String.Format(this.GeoClientAPIURL + "{0}?{1}", this.queryParams.searchFile, this.queryParams.searchObject.getParametersURL());
                }
                #endregion

                #region Parse outSR
                this.queryParams.outWkid = 4326;
                this.queryParams.outLatestWkid = 4326;

                if (d.ContainsKey("OUTSR") && d["OUTSR"] != null)
                {
                    int wkid = 0, latestWkid = 0;
                    if (Int32.TryParse(d["OUTSR"], out wkid))
                    {
                        this.queryParams.outWkid = wkid;
                        this.queryParams.outLatestWkid = (wkid == 102100) ? 3857 : wkid;
                    }
                    else
                    {
                        char[] separators = { ':', ',' };
                        string sr = d["OUTSR"].Replace("{", "").Replace("}", "").Replace("\"","").Replace("'","");
                        string[] pairs = sr.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < pairs.Length; i++)
                        {
                            if (pairs[i] == "wkid")
                                wkid = Int32.Parse(pairs[i+1]);
                            if (pairs[i] == "latestWkid")
                                latestWkid = Int32.Parse(pairs[i + 1]);
                        }

                        this.queryParams.outWkid = (wkid > 0) ? wkid : ((latestWkid > 0) ? latestWkid : 4326);
                        this.queryParams.outLatestWkid = (latestWkid > 0) ? latestWkid : this.queryParams.outWkid;
                    }
                }
                #endregion

                #region Parse callback
                var matches2 = query.Where(kv => kv.Key.ToLower().IndexOf("callback") > -1);
                if (matches2.Count() > 0)
                {
                    this.queryParams.callback = matches2.First().Value;
                }
                #endregion
            }

            if (errMsg != "")
            {
                if (this.queryParams.callback != null) errMsg = this.queryParams.callback + "(" + errMsg + ");";
                var responseMsg = new HttpResponseMessage(HttpStatusCode.BadRequest);
                responseMsg.Content = new StringContent(errMsg, System.Text.Encoding.UTF8, "text/json");
                return responseMsg;
            }
            else
            {
                return sendRequest(action);
            }
        }

        private HttpResponseMessage sendRequest(string action)
        {
            string result = "";

            if (!string.IsNullOrEmpty(this.queryParams.searchURL))
            {
                var appSettings = ConfigurationManager.AppSettings;
                string sAuth = string.Format("&app_id={0}&app_key={1}", appSettings["app_id"], appSettings["app_key"]);
                HttpWebRequest request = WebRequest.CreateHttp(this.queryParams.searchURL + sAuth);

                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    Stream responseStream = copyStream(response.GetResponseStream());

                    if (this.queryParams.searchObject.GetType() == typeof(SingleInputSearch))
                    {
                        DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(SearchResponse));
                        SearchResponse searchResponse = (SearchResponse)serializer.ReadObject(responseStream);
                        SearchResultAbstract[] searchResults = searchResponse.results;
                        if (searchResults == null || searchResults.Length == 0)
                        {
                            result = "{error: {status:\"rejected\", message: \"Your search is rejected. No places were found to match your input\"}}";
                        }
                        else
                        {
                            SearchResultAbstract searchResult = searchResults[0];

                            responseStream.Position = 0;
                            string requestMatch = searchResult.request.ToUpper();
                            if (requestMatch.StartsWith("PLACE"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponsePlace));
                                SearchResponsePlace response2 = (SearchResponsePlace)serializer2.ReadObject(responseStream);
                                Place place = response2.results[0].response;
                                result = (action == "find") ? serializeFindResult(place) : serializeCandidates(place);
                            }
                            else if (requestMatch.StartsWith("BBL"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseBBL));
                                SearchResponseBBL response2 = (SearchResponseBBL)serializer2.ReadObject(responseStream);
                                BBL bbl = response2.results[0].response;
                                result = (action == "find") ? serializeFindResult(bbl) : serializeCandidates(bbl);
                            }
                            else if (requestMatch.StartsWith("BIN"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseBIN));
                                SearchResponseBIN response2 = (SearchResponseBIN)serializer2.ReadObject(responseStream);
                                BIN bin = response2.results[0].response;
                                result = (action == "find") ? serializeFindResult(bin) : serializeCandidates(bin);
                            }
                            else if (requestMatch.StartsWith("BLOCKFACE"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseBlockFace));
                                SearchResponseBlockFace response2 = (SearchResponseBlockFace)serializer2.ReadObject(responseStream);
                                BlockFace blockface = response2.results[0].response;
                                result = (action == "find") ? serializeFindResult(blockface) : serializeCandidates(blockface);
                            }
                            else if (requestMatch.StartsWith("INTERSECTION"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseIntersection));
                                SearchResponseIntersection response2 = (SearchResponseIntersection)serializer2.ReadObject(responseStream);
                                Intersection intersect = response2.results[0].response;
                                result = (action == "find") ? serializeFindResult(intersect) : serializeCandidates(intersect);
                            }
                            else if (requestMatch.StartsWith("ADDRESS"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseAddress));
                                SearchResponseAddress response2 = (SearchResponseAddress)serializer2.ReadObject(responseStream);
                                Address address = response2.results[0].response;
                                result = (action == "find") ? serializeFindResult(address) : serializeCandidates(address);
                            }
                        }
                    }
                    else if (this.queryParams.searchObject.GetType() == typeof(PlaceSearch))
                    {
                        DataContractJsonSerializer placeSerializer = new DataContractJsonSerializer(typeof(PlaceResult));
                        PlaceResult placeResult = (PlaceResult)placeSerializer.ReadObject(responseStream);
                        result = (action == "find") ? serializeFindResult(placeResult.place) : serializeCandidates(placeResult.place);
                    }
                    else if (this.queryParams.searchObject.GetType() == typeof(BBLSearch))
                    {
                        DataContractJsonSerializer BBLSerializer = new DataContractJsonSerializer(typeof(BBLResult));
                        BBLResult bblResult = (BBLResult)BBLSerializer.ReadObject(responseStream);
                        result = (action == "find") ? serializeFindResult(bblResult.bbl) : serializeCandidates(bblResult.bbl);
                    }
                    else if (this.queryParams.searchObject.GetType() == typeof(BINSearch))
                    {
                        DataContractJsonSerializer BinSerializer = new DataContractJsonSerializer(typeof(BINResult));
                        BINResult binResult = (BINResult)BinSerializer.ReadObject(responseStream);
                        result = (action == "find") ? serializeFindResult(binResult.bin) : serializeCandidates(binResult.bin);
                    }
                    else if (this.queryParams.searchObject.GetType() == typeof(BlockFaceSearch))
                    {
                        DataContractJsonSerializer BlockFaceSerializer = new DataContractJsonSerializer(typeof(BlockFaceResult));
                        BlockFaceResult blockFaceResult = (BlockFaceResult)BlockFaceSerializer.ReadObject(responseStream);
                        result = (action == "find") ? serializeFindResult(blockFaceResult.blockface) : serializeCandidates(blockFaceResult.blockface);
                    }
                    else if (this.queryParams.searchObject.GetType() == typeof(IntersectionSearch))
                    {
                        DataContractJsonSerializer IntersectionSerializer = new DataContractJsonSerializer(typeof(IntersectionResult));
                        IntersectionResult intersectionResult = (IntersectionResult)IntersectionSerializer.ReadObject(responseStream);
                        result = (action == "find") ? serializeFindResult(intersectionResult.intersection) : serializeCandidates(intersectionResult.intersection);
                    }
                    else if (this.queryParams.searchObject.GetType() == typeof(AddressSearch))
                    {
                        DataContractJsonSerializer addressSerializer = new DataContractJsonSerializer(typeof(AddressResult));
                        AddressResult addressResult = (AddressResult)addressSerializer.ReadObject(responseStream);
                        result = (action == "find") ? serializeFindResult(addressResult.address) : serializeCandidates(addressResult.address);
                    }
                    else
                    {
                        StreamReader reader = new StreamReader(responseStream);
                        result = reader.ReadToEnd();
                    }
                }
            }

            if (this.queryParams.callback != null) result = this.queryParams.callback + "(" + result + ");";
            var responseMsg = new HttpResponseMessage(HttpStatusCode.OK);

            responseMsg.Content = new StringContent(result, System.Text.Encoding.UTF8, "text/json");
            return responseMsg;
        }
        #endregion

        #region serialize results into JSON - for "findAddressCandidates" operation
        private string serializeCandidates(object result)
        {
            double lat = 0, lon = 0;
            string address = "", locType = "GeoClient";

            SpatialReference spatialReference = new SpatialReference();
            spatialReference.wkid = this.queryParams.outWkid;
            spatialReference.latestWkid = this.queryParams.outLatestWkid;

            switch (result.GetType().Name)
            {
                case "Place":
                    lat = (result as Place).latitude;
                    lon = (result as Place).longitude;
                    address = (result as Place).firstStreetNameNormalized + ", " + (result as Place).firstBoroughName;
                    locType += " Place";
                    break;
                case "BBL":
                    lat = (result as BBL).latitudeInternalLabel;
                    lon = (result as BBL).longitudeInternalLabel;
                    address = (result as BBL).bbl + ", " + (result as BBL).firstBoroughName;
                    locType += " BBL";
                    break;
                case "BIN":
                    lat = (result as BIN).latitudeInternalLabel;
                    lon = (result as BIN).longitudeInternalLabel;
                    address = (result as BIN).buildingIdentificationNumber + ", " + (result as BIN).firstBoroughName;
                    locType += " BIN";
                    break;
                case "BlockFace":
                    lat = (result as BlockFace).latitude; // GeoClient BlockFace search does not return latitude/longitude
                    lon = (result as BlockFace).longitude;
                    address = (result as BlockFace).firstStreetNameNormalized + ", " + (result as BlockFace).firstBoroughName;
                    locType += " BlockFace";
                    break;
                case "Address":
                    lat = (result as Address).latitude;
                    lon = (result as Address).longitude;
                    address = (result as Address).houseNumber + " " + (result as Address).firstStreetNameNormalized + ", " + (result as Address).firstBoroughName;
                    locType += " Address";
                    break;
                case "Intersection":
                    lat = (result as Intersection).latitude;
                    lon = (result as Intersection).longitude;
                    address = (result as Intersection).firstStreetNameNormalized + " and " + (result as Intersection).secondStreetNameNormalized + ", " + (result as Intersection).firstBoroughName;
                    locType += " Intersection";
                    break;
            }

            if (lat == 0 || lon == 0)
            {
                return "{error: {status:\"failed\", message: \"GeoClient does not return geographic coordinates for this search\"}}";
            }
            else
            {
                Candidate candidate = new Candidate();
                candidate.score = 100;
                candidate.address = address;
                dynamic attributes = new ExpandoObject();
                attributes.Loc_name = locType;
                Utils.CopyProperties(result, attributes);
                candidate.attributes = attributes;
                candidate.location = ensureWebMeractor(lon, lat);
                GeocoderJsonResult geoResult = new GeocoderJsonResult();
                geoResult.spatialReference = spatialReference;
                geoResult.candidates = new List<Candidate>();
                geoResult.candidates.Add(candidate);

                //MemoryStream mstream = new MemoryStream();
                //DataContractJsonSerializer ser2 = new DataContractJsonSerializer(geoResult.GetType());
                //ser2.WriteObject(mstream, geoResult);

                //mstream.Position = 0;
                //StreamReader sr = new StreamReader(mstream);
                //string json = sr.ReadToEnd();
                string json = JsonConvert.SerializeObject(geoResult);
                return json;
            }
        }
        #endregion

        #region serialize results into JSON - for "find" operation
        private string serializeFindResult(object found)
        {
            double lat = 0, lon = 0;
            string locName = "", locType = "GeoClient";

            ExplorerJsonResult fResult = new ExplorerJsonResult();
            fResult.spatialReference = new SpatialReference();
            fResult.spatialReference.wkid = this.queryParams.outWkid;
            fResult.spatialReference.latestWkid = this.queryParams.outLatestWkid;

            switch (found.GetType().Name)
            {
                case "Place":
                    lat = (found as Place).latitude;
                    lon = (found as Place).longitude;
                    locName = (found as Place).firstStreetNameNormalized + ", " + (found as Place).firstBoroughName;
                    locType += " Place";
                    break;
                case "BBL":
                    lat = (found as BBL).latitudeInternalLabel;
                    lon = (found as BBL).longitudeInternalLabel;
                    locName = (found as BBL).bbl + ", " + (found as BBL).firstBoroughName;
                    locType += " BBL";
                    break;
                case "BIN":
                    lat = (found as BIN).latitudeInternalLabel;
                    lon = (found as BIN).longitudeInternalLabel;
                    locName = (found as BIN).buildingIdentificationNumber + ", " + (found as BIN).firstBoroughName;
                    locType += " BIN";
                    break;
                case "BlockFace":
                    lat = (found as BlockFace).latitude;
                    lon = (found as BlockFace).longitude;
                    locName = (found as BlockFace).firstStreetNameNormalized + ", " + (found as BlockFace).firstBoroughName;
                    locType += " BlockFace";
                    break;
                case "Intersection":
                    lat = (found as Intersection).latitude;
                    lon = (found as Intersection).longitude;
                    locName = (found as Intersection).firstStreetNameNormalized + " and " + (found as Intersection).secondStreetNameNormalized + ", " + (found as Intersection).firstBoroughName;
                    locType += " Intersection";
                    break;
                case "Address":
                    lat = (found as Address).latitude;
                    lon = (found as Address).longitude;
                    locName = (found as Address).houseNumber + " " + (found as Address).firstStreetNameNormalized + ", " + (found as Address).firstBoroughName;
                    locType += " Address";
                    break;
            }

            if (lat == 0 || lon == 0)
            {
                return "{error: {status:\"failed\", message: \"GeoClient does not return geographic coordinates for this search\"}}";
            }
            else
            {
                CandidateLocation location = new CandidateLocation();
                location.name = locName;
                location.extent = new Extent();
                location.feature = new Feature();

                //get WebMercator
                Geometry geo = ensureWebMeractor(lon, lat);
                location.feature.geometry = geo;

                location.extent.xmin = (this.queryParams.outWkid == 4326) ? (geo.x - 0.001) : (geo.x - 110);
                location.extent.xmax = (this.queryParams.outWkid == 4326) ? (geo.x + 0.001) : (geo.x + 110);
                location.extent.ymin = (this.queryParams.outWkid == 4326) ? (geo.y - 0.001) : (geo.y - 130);
                location.extent.ymax = (this.queryParams.outWkid == 4326) ? (geo.y + 0.001) : (geo.y + 130);

                dynamic attr = new ExpandoObject();
                attr.score = 100;
                attr.Addr_Type = locType;
                Utils.CopyProperties(found, attr);
                location.feature.attributes = attr;

                fResult.locations = new List<CandidateLocation>();
                fResult.locations.Add(location);

                //MemoryStream mstream = new MemoryStream();
                //DataContractJsonSerializer ser2 = new DataContractJsonSerializer(typeof(ExplorerJsonResult));
                //ser2.WriteObject(mstream, fResult);

                //mstream.Position = 0;
                //StreamReader sr = new StreamReader(mstream);
                //string result = sr.ReadToEnd();
                string json = JsonConvert.SerializeObject(fResult);
                return json;
            }
        }
        #endregion

        private double ensureLatitude(double y1, double y2)
        {
            //Hello Registered Geoclient User,

            //A bug was recently discovered in the Geoclient service in which values for the lat/long-related coordinates are reversed. Specifically, this bug affects the following fields:

            //latitude/longitude
            //latitudeInternalLabel/longitudeInternalLabel

            //Please note that the following fields are correct and will not change: xCoordinate, yCoordinate, internalLabelXCoordinate, internalLabelYCoordinate

            //On January 8th, 2014, DoITT GIS will be releasing a patch to the Geoclient service which corrects this bug. Depending on your current use of these values, this change may impact your application.

            if (y1 < 0) return y2;
            return y1;
        }

        private double ensureLongitude(double x1, double x2)
        {

            // Hello Registered Geoclient User,

            //A bug was recently discovered in the Geoclient service in which values for the lat/long-related coordinates are reversed. Specifically, this bug affects the following fields:

            //latitude/longitude
            //latitudeInternalLabel/longitudeInternalLabel

            //Please note that the following fields are correct and will not change: xCoordinate, yCoordinate, internalLabelXCoordinate, internalLabelYCoordinate

            //On January 8th, 2014, DoITT GIS will be releasing a patch to the Geoclient service which corrects this bug. Depending on your current use of these values, this change may impact your application.

            if (x1 < 0) return x1;
            return x2;
        }

        private Geometry ensureWebMeractor(double x, double y)
        {
            Geometry g = new Geometry();
            g.x = ensureLongitude(x, y);
            g.y = ensureLatitude(y, x);

            if (x != 0 && y != 0)
            {
                HttpWebRequest requestSR = WebRequest.CreateHttp("http://tasks.arcgisonline.com/ArcGIS/rest/services/Geometry/GeometryServer/project?inSR=4326&outSR=" + this.queryParams.outWkid + "&f=json&geometries=" + x + "," + y);

                using (HttpWebResponse responseSR = requestSR.GetResponse() as HttpWebResponse)
                {
                    Stream responseSRStream = responseSR.GetResponseStream();

                    DataContractJsonSerializer serSR = new DataContractJsonSerializer(typeof(GeometryResult));
                    GeometryResult gR = (GeometryResult)serSR.ReadObject(responseSRStream);
                    g.x = gR.geometries[0].x;
                    g.y = gR.geometries[0].y;

                    return g;
                }

            }

            return g;
        }

        private Dictionary<string, string> doParse(string s)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();

            try
            {
                //C# code challenge.  Break this free text into key value pairs of some sort:

                //Name:boston housenumber:314 street:west 100 st bin:2123 block:110 place:empire state building

                //Name | boston
                //Housenumber | 314
                //Street | west 100 st
                //Bin | 2123
                //Block |110
                //Place | empire state building


                //string s = "Name:boston housenumber:314 street:west 100 st bin:2123 block:110 place:empire state building";
                char sChar = ' ';
                char cChar = ':';
                string[] sa = s.Split(sChar);
                string sKey = null;
                string sVal = "";
                foreach (string elm in sa)
                {
                    if (elm.Contains(":"))
                    {
                        if (sKey != null)
                        {
                            d.Add(sKey, sVal.Trim());
                            sVal = "";
                        }

                        sKey = elm.Split(cChar)[0];
                        sVal += " " + elm.Split(cChar)[1];
                    }
                    else
                    {
                        sVal += " " + elm;
                    }
                }

                d.Add(sKey, sVal.Trim());
            }
            catch (Exception ex)
            {

            }

            return d;
        }

        private static Stream copyStream(Stream st)
        {
            const int readSize = 256;
            byte[] buffer = new byte[readSize];
            MemoryStream ms = new MemoryStream();

            int count = st.Read(buffer, 0, readSize);
            while (count > 0)
            {
                ms.Write(buffer, 0, count);
                count = st.Read(buffer, 0, readSize);
            }
            ms.Position = 0;
            st.Close();
            return ms;
        }

        // GET api/GeocodeServer/5
        public string Get(int id)
        {
            return "value";
        }

        // POST api/GeocodeServer
        public void Post([FromBody]string value)
        {
        }

        // PUT api/GeocodeServer/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE api/GeocodeServer/5
        public void Delete(int id)
        {
        }
    }

    #region Single Field Search Result Objects
    public class SearchResponse
    {
        public string status { get; set; }
        public string input { get; set; }
        public SearchResultAbstract[] results { get; set; }
    }

    public class SearchResultAbstract
    {
        public string level { get; set; }
        public string status { get; set; }
        public string request { get; set; }
    }

    public class SearchResponsePlace
    {
        public SearchResultPlace[] results { get; set; }
    }

    public class SearchResultPlace
    {
        public Place response { get; set; }
    }

    public class SearchResponseBBL
    {
        public SearchResultBBL[] results { get; set; }
    }

    public class SearchResultBBL
    {
        public BBL response { get; set; }
    }

    public class SearchResponseBIN
    {
        public SearchResultBIN[] results { get; set; }
    }

    public class SearchResultBIN
    {
        public BIN response { get; set; }
    }

    public class SearchResponseBlockFace
    {
        public SearchResultBlockFace[] results { get; set; }
    }

    public class SearchResultBlockFace
    {
        public BlockFace response { get; set; }
    }

    public class SearchResponseAddress
    {
        public SearchResultAddress[] results { get; set; }
    }

    public class SearchResultAddress
    {
        public Address response { get; set; }
    }

    public class SearchResponseIntersection
    {
        public SearchResultIntersection[] results { get; set; }
    }

    public class SearchResultIntersection
    {
        public Intersection response { get; set; }
    }
    #endregion

    #region Other Geocoding Result Objects
    public class Place
    {
        public string assemblyDistrict { get; set; }
        //public string attributeBytes { get; set; }
        public string bbl { get; set; }
        public string bblBoroughCode { get; set; }
        public string bblTaxBlock { get; set; }
        public string bblTaxLot { get; set; }
        public string boeLgcPointer { get; set; }
        public string boePreferredStreetName { get; set; }
        public string boePreferredstreetCode { get; set; }
        public string boroughCode1In { get; set; }
        public string buildingIdentificationNumber { get; set; }
        public string businessImprovementDistrict { get; set; }
        public string censusBlock2000 { get; set; }
        public string censusBlock2010 { get; set; }
        public string censusTract1990 { get; set; }
        public string censusTract2000 { get; set; }
        public string censusTract2010 { get; set; }
        public string cityCouncilDistrict { get; set; }
        public string civilCourtDistrict { get; set; }
        public string coincidenceSegmentCount { get; set; }
        public string communityDistrict { get; set; }
        public string communityDistrictBoroughCode { get; set; }
        public string communityDistrictNumber { get; set; }
        public string communitySchoolDistrict { get; set; }
        public string condominiumBillingBbl { get; set; }
        public string congressionalDistrict { get; set; }
        public string cooperativeIdNumber { get; set; }
        public string cornerCode { get; set; }
        public string crossStreetNamesFlagIn { get; set; }
        public string dcpCommercialStudyArea { get; set; }
        public string dcpPreferredLgc { get; set; }
        public string dotStreetLightContractorArea { get; set; }
        public string dynamicBlock { get; set; }
        public string electionDistrict { get; set; }
        public string fireBattalion { get; set; }
        public string fireCompanyNumber { get; set; }
        public string fireCompanyType { get; set; }
        public string fireDivision { get; set; }
        public string firstBoroughName { get; set; }
        public string firstStreetCode { get; set; }
        public string firstStreetNameNormalized { get; set; }
        public string fromLionNodeId { get; set; }
        public string fromPreferredLgcsFirstSetOf5 { get; set; }
        public string genericId { get; set; }
        public string geosupportFunctionCode { get; set; }
        public string geosupportReturnCode { get; set; }
        public string geosupportReturnCode2 { get; set; }
        public string gi5DigitStreetCode1 { get; set; }
        public string gi5DigitStreetCode2 { get; set; }
        public string gi5DigitStreetCode3 { get; set; }
        public string gi5DigitStreetCode4 { get; set; }
        public string giBoroughCode1 { get; set; }
        public string giBoroughCode2 { get; set; }
        public string giBoroughCode3 { get; set; }
        public string giBoroughCode4 { get; set; }
        public string giBuildingIdentificationNumber1 { get; set; }
        public string giBuildingIdentificationNumber2 { get; set; }
        public string giBuildingIdentificationNumber3 { get; set; }
        public string giBuildingIdentificationNumber4 { get; set; }
        public string giDcpPreferredLgc1 { get; set; }
        public string giDcpPreferredLgc2 { get; set; }
        public string giDcpPreferredLgc3 { get; set; }
        public string giDcpPreferredLgc4 { get; set; }
        public string giGeographicIdentifier1 { get; set; }
        public string giHighHouseNumber2 { get; set; }
        public string giHighHouseNumber3 { get; set; }
        public string giHighHouseNumber4 { get; set; }
        public string giLowHouseNumber2 { get; set; }
        public string giLowHouseNumber3 { get; set; }
        public string giLowHouseNumber4 { get; set; }
        public string giSideOfStreetIndicator1 { get; set; }
        public string giSideOfStreetIndicator2 { get; set; }
        public string giSideOfStreetIndicator3 { get; set; }
        public string giSideOfStreetIndicator4 { get; set; }
        public string giStreetCode1 { get; set; }
        public string giStreetCode2 { get; set; }
        public string giStreetCode3 { get; set; }
        public string giStreetCode4 { get; set; }
        public string giStreetName1 { get; set; }
        public string giStreetName2 { get; set; }
        public string giStreetName3 { get; set; }
        public string giStreetName4 { get; set; }
        public string healthArea { get; set; }
        public string healthCenterDistrict { get; set; }
        public string highBblOfThisBuildingsCondominiumUnits { get; set; }
        public string highCrossStreetB5SC1 { get; set; }
        public string highCrossStreetB5SC2 { get; set; }
        public string highCrossStreetCode1 { get; set; }
        public string highCrossStreetCode2 { get; set; }
        public string highCrossStreetName1 { get; set; }
        public string highCrossStreetName2 { get; set; }
        public string highHouseNumberOfBlockFaceSortFormat { get; set; }
        public string interimAssistanceEligibilityIndicator { get; set; }
        public string internalLabelXCoordinate { get; set; }
        public string internalLabelYCoordinate { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string legacySegmentId { get; set; }
        public string lionKeyBoroughCode { get; set; }
        public string lionKeyFaceCode { get; set; }
        public string lionKeyForVanityAddressBoroughCode { get; set; }
        public string lionKeyForVanityAddressFaceCode { get; set; }
        public string lionKeyForVanityAddressSequenceNumber { get; set; }
        public string lionKeySequenceNumber { get; set; }
        public string listOf4Lgcs { get; set; }
        public string lowBblOfThisBuildingsCondominiumUnits { get; set; }
        public string lowCrossStreetB5SC1 { get; set; }
        public string lowCrossStreetB5SC2 { get; set; }
        public string lowCrossStreetCode1 { get; set; }
        public string lowCrossStreetCode2 { get; set; }
        public string lowCrossStreetName1 { get; set; }
        public string lowCrossStreetName2 { get; set; }
        public string lowHouseNumberOfBlockFaceSortFormat { get; set; }
        public string lowHouseNumberOfDefiningAddressRange { get; set; }
        public string message { get; set; }
        public string nta { get; set; }
        public string ntaName { get; set; }
        public string numberOfCrossStreetB5SCsHighAddressEnd { get; set; }
        public string numberOfCrossStreetB5SCsLowAddressEnd { get; set; }
        public string numberOfCrossStreetsHighAddressEnd { get; set; }
        public string numberOfCrossStreetsLowAddressEnd { get; set; }
        public string numberOfEntriesInListOfGeographicIdentifiers { get; set; }
        public string numberOfExistingStructuresOnLot { get; set; }
        public string numberOfStreetFrontagesOfLot { get; set; }
        public string physicalId { get; set; }
        public string policePatrolBoroughCommand { get; set; }
        public string policePrecinct { get; set; }
        public string reasonCode { get; set; }
        public string reasonCode1e { get; set; }
        public string returnCode1a { get; set; }
        public string returnCode1e { get; set; }
        public string roadwayType { get; set; }
        public string rpadBuildingClassificationCode { get; set; }
        public string rpadSelfCheckCodeForBbl { get; set; }
        public string sanbornBoroughCode { get; set; }
        public string sanbornPageNumber { get; set; }
        public string sanbornVolumeNumber { get; set; }
        public string sanitationCollectionSchedulingSectionAndSubsection { get; set; }
        public string sanitationDistrict { get; set; }
        public string sanitationRecyclingCollectionSchedule { get; set; }
        public string sanitationRegularCollectionSchedule { get; set; }
        public string sanitationSnowPriorityCode { get; set; }
        public string segmentAzimuth { get; set; }
        public string segmentIdentifier { get; set; }
        public string segmentLengthInFeet { get; set; }
        public string segmentOrientation { get; set; }
        public string segmentTypeCode { get; set; }
        public string sideOfStreetIndicator { get; set; }
        public string sideOfStreetOfVanityAddress { get; set; }
        public string specialAddressGeneratedRecordFlag { get; set; }
        public string splitLowHouseNumber { get; set; }
        public string stateSenatorialDistrict { get; set; }
        public string streetAttributeIndicator { get; set; }
        public string streetName1In { get; set; }
        public string streetStatus { get; set; }
        public string taxMapNumberSectionAndVolume { get; set; }
        public string toLionNodeId { get; set; }
        public string toPreferredLgcsFirstSetOf5 { get; set; }
        public string trafficDirection { get; set; }
        public string underlyingHnsOnTrueStreet { get; set; }
        public string underlyingstreetCode { get; set; }
        public string workAreaFormatIndicatorIn { get; set; }
        public string xCoordinate { get; set; }
        public string xCoordinateHighAddressEnd { get; set; }
        public string xCoordinateLowAddressEnd { get; set; }
        public string xCoordinateOfCenterofCurvature { get; set; }
        public string yCoordinate { get; set; }
        public string yCoordinateHighAddressEnd { get; set; }
        public string yCoordinateLowAddressEnd { get; set; }
        public string yCoordinateOfCenterofCurvature { get; set; }
        public string zipCode { get; set; }
    }

    public class PlaceResult
    {
        public Place place { get; set; }
    }

    public class BBL
    {
        public string bbl { get; set; }
        public string bblBoroughCode { get; set; }
        public string bblBoroughCodeIn { get; set; }
        public string bblTaxBlock { get; set; }
        public string bblTaxBlockIn { get; set; }
        public string bblTaxLot { get; set; }
        public string bblTaxLotIn { get; set; }
        public string buildingIdentificationNumber { get; set; }
        public string condominiumBillingBbl { get; set; }
        public string cooperativeIdNumber { get; set; }
        public string cornerCode { get; set; }
        public string crossStreetNamesFlagIn { get; set; }
        public string firstBoroughName { get; set; }
        public string geosupportFunctionCode { get; set; }
        public string geosupportReturnCode { get; set; }
        public string gi5DigitStreetCode1 { get; set; }
        public string gi5DigitStreetCode2 { get; set; }
        public string giBoroughCode1 { get; set; }
        public string giBoroughCode2 { get; set; }
        public string giBuildingIdentificationNumber1 { get; set; }
        public string giBuildingIdentificationNumber2 { get; set; }
        public string giDcpPreferredLgc1 { get; set; }
        public string giDcpPreferredLgc2 { get; set; }
        public string giGeographicIdentifier1 { get; set; }
        public string giHighHouseNumber1 { get; set; }
        public string giHighHouseNumber2 { get; set; }
        public string giLowHouseNumber1 { get; set; }
        public string giLowHouseNumber2 { get; set; }
        public string giSideOfStreetIndicator1 { get; set; }
        public string giSideOfStreetIndicator2 { get; set; }
        public string giStreetCode1 { get; set; }
        public string giStreetCode2 { get; set; }
        public string highBblOfThisBuildingsCondominiumUnits { get; set; }
        public string internalLabelXCoordinate { get; set; }
        public string internalLabelYCoordinate { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public double latitudeInternalLabel { get; set; }
        public double longitudeInternalLabel { get; set; }
        public string lowBblOfThisBuildingsCondominiumUnits { get; set; }
        public string lowHouseNumberOfDefiningAddressRange { get; set; }
        public string numberOfEntriesInListOfGeographicIdentifiers { get; set; }
        public string numberOfExistingStructuresOnLot { get; set; }
        public string numberOfStreetFrontagesOfLot { get; set; }
        public string rpadBuildingClassificationCode { get; set; }
        public string rpadSelfCheckCodeForBbl { get; set; }
        public string sanbornBoroughCode { get; set; }
        public string sanbornPageNumber { get; set; }
        public string sanbornVolumeNumber { get; set; }
        public string sanbornVolumeNumberSuffix { get; set; }
        public string taxMapNumberSectionAndVolume { get; set; }
        public string workAreaFormatIndicatorIn { get; set; }
    }

    public class BBLResult
    {
        public BBL bbl { get; set; }
    }

    public class BIN
    {
        public string bbl { get; set; }
        public string bblBoroughCode { get; set; }
        public string bblTaxBlock { get; set; }
        public string bblTaxLot { get; set; }
        public string buildingIdentificationNumber { get; set; }
        public string buildingIdentificationNumberIn { get; set; }
        public string condominiumBillingBbl { get; set; }
        public string cooperativeIdNumber { get; set; }
        public string cornerCode { get; set; }
        public string crossStreetNamesFlagIn { get; set; }
        public string dcpCommercialStudyArea { get; set; }
        public string firstBoroughName { get; set; }
        public string geosupportFunctionCode { get; set; }
        public string geosupportReturnCode { get; set; }
        public string gi5DigitStreetCode1 { get; set; }
        public string gi5DigitStreetCode2 { get; set; }
        public string gi5DigitStreetCode3 { get; set; }
        public string gi5DigitStreetCode4 { get; set; }
        public string giBoroughCode1 { get; set; }
        public string giBoroughCode2 { get; set; }
        public string giBoroughCode3 { get; set; }
        public string giBoroughCode4 { get; set; }
        public string giBuildingIdentificationNumber1 { get; set; }
        public string giBuildingIdentificationNumber2 { get; set; }
        public string giBuildingIdentificationNumber3 { get; set; }
        public string giBuildingIdentificationNumber4 { get; set; }
        public string giDcpPreferredLgc1 { get; set; }
        public string giDcpPreferredLgc2 { get; set; }
        public string giDcpPreferredLgc3 { get; set; }
        public string giDcpPreferredLgc4 { get; set; }
        public string giHighHouseNumber1 { get; set; }
        public string giHighHouseNumber2 { get; set; }
        public string giHighHouseNumber3 { get; set; }
        public string giHighHouseNumber4 { get; set; }
        public string giLowHouseNumber1 { get; set; }
        public string giLowHouseNumber2 { get; set; }
        public string giLowHouseNumber3 { get; set; }
        public string giLowHouseNumber4 { get; set; }
        public string giSideOfStreetIndicator1 { get; set; }
        public string giSideOfStreetIndicator2 { get; set; }
        public string giSideOfStreetIndicator3 { get; set; }
        public string giSideOfStreetIndicator4 { get; set; }
        public string giStreetCode1 { get; set; }
        public string giStreetCode2 { get; set; }
        public string giStreetCode3 { get; set; }
        public string giStreetCode4 { get; set; }
        public string highBblOfThisBuildingsCondominiumUnits { get; set; }
        public string internalLabelXCoordinate { get; set; }
        public string internalLabelYCoordinate { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public double latitudeInternalLabel { get; set; }
        public double longitudeInternalLabel { get; set; }
        public string lowBblOfThisBuildingsCondominiumUnits { get; set; }
        public string lowHouseNumberOfDefiningAddressRange { get; set; }
        public string numberOfEntriesInListOfGeographicIdentifiers { get; set; }
        public string numberOfExistingStructuresOnLot { get; set; }
        public string numberOfStreetFrontagesOfLot { get; set; }
        public string rpadBuildingClassificationCode { get; set; }
        public string rpadSelfCheckCodeForBbl { get; set; }
        public string sanbornBoroughCode { get; set; }
        public string sanbornPageNumber { get; set; }
        public string sanbornVolumeNumber { get; set; }
        public string sanbornVolumeNumberSuffix { get; set; }
        public string taxMapNumberSectionAndVolume { get; set; }
        public string workAreaFormatIndicatorIn { get; set; }
    }

    public class BINResult
    {
        public BIN bin { get; set; }
    }

    public class BlockFace
    {
        public double latitude { get; set; }
        public double longitude { get; set; }

        public string boroughCode1In { get; set; }
        public string coincidentSegmentCount { get; set; }
        public string crossStreetNamesFlagIn { get; set; }
        public string dcpPreferredLgcForStreet1 { get; set; }
        public string dcpPreferredLgcForStreet2 { get; set; }
        public string dcpPreferredLgcForStreet3 { get; set; }
        public string dotStreetLightContractorArea { get; set; }
        public string firstBoroughName { get; set; }
        public string firstStreetCode { get; set; }
        public string firstStreetNameNormalized { get; set; }
        public string fromLgc1 { get; set; }
        public string fromLgc2 { get; set; }
        public string fromNode { get; set; }
        public string fromXCoordinate { get; set; }
        public string fromYCoordinate { get; set; }
        public string generatedRecordFlag { get; set; }
        public string genericId { get; set; }
        public string geosupportFunctionCode { get; set; }
        public string geosupportReturnCode { get; set; }
        public string highCrossStreetB5SC1 { get; set; }
        public string leftSegment1990CensusTract { get; set; }
        public string leftSegment2000CensusBlock { get; set; }
        public string leftSegment2000CensusTract { get; set; }
        public string leftSegment2010CensusBlock { get; set; }
        public string leftSegment2010CensusTract { get; set; }
        public string leftSegmentAssemblyDistrict { get; set; }
        public string leftSegmentCommunityDistrict { get; set; }
        public string leftSegmentCommunityDistrictBoroughCode { get; set; }
        public string leftSegmentCommunityDistrictNumber { get; set; }
        public string leftSegmentCommunitySchoolDistrict { get; set; }
        public string leftSegmentDynamicBlock { get; set; }
        public string leftSegmentElectionDistrict { get; set; }
        public string leftSegmentFireBattalion { get; set; }
        public string leftSegmentFireCompanyNumber { get; set; }
        public string leftSegmentFireCompanyType { get; set; }
        public string leftSegmentFireDivision { get; set; }
        public string leftSegmentHealthArea { get; set; }
        public string leftSegmentHealthCenterDistrict { get; set; }
        public string leftSegmentHighHouseNumber { get; set; }
        public string leftSegmentInterimAssistanceEligibilityIndicator { get; set; }
        public string leftSegmentLowHouseNumber { get; set; }
        public string leftSegmentNta { get; set; }
        public string leftSegmentNtaName { get; set; }
        public string leftSegmentPolicePatrolBoroughCommand { get; set; }
        public string leftSegmentPolicePrecinct { get; set; }
        public string leftSegmentZipCode { get; set; }
        public string legacyId { get; set; }
        public string lengthOfSegmentInFeet { get; set; }
        public string lgc1 { get; set; }
        public string lionBoroughCode { get; set; }
        public string lionFaceCode { get; set; }
        public string lionKey { get; set; }
        public string lionSequenceNumber { get; set; }
        public string locationalStatusOfSegment { get; set; }
        public string lowCrossStreetB5SC1 { get; set; }
        public string modeSwitchIn { get; set; }
        public string numberOfCrossStreetB5SCsHighAddressEnd { get; set; }
        public string numberOfCrossStreetB5SCsLowAddressEnd { get; set; }
        public string numberOfStreetCodesAndNamesInList { get; set; }
        public string physicalId { get; set; }
        public string rightSegment1990CensusTract { get; set; }
        public string rightSegment2000CensusBlock { get; set; }
        public string rightSegment2000CensusTract { get; set; }
        public string rightSegment2010CensusBlock { get; set; }
        public string rightSegment2010CensusTract { get; set; }
        public string rightSegmentAssemblyDistrict { get; set; }
        public string rightSegmentCommunityDistrict { get; set; }
        public string rightSegmentCommunityDistrictBoroughCode { get; set; }
        public string rightSegmentCommunityDistrictNumber { get; set; }
        public string rightSegmentCommunitySchoolDistrict { get; set; }
        public string rightSegmentDynamicBlock { get; set; }
        public string rightSegmentElectionDistrict { get; set; }
        public string rightSegmentFireBattalion { get; set; }
        public string rightSegmentFireCompanyNumber { get; set; }
        public string rightSegmentFireCompanyType { get; set; }
        public string rightSegmentFireDivision { get; set; }
        public string rightSegmentHealthArea { get; set; }
        public string rightSegmentHealthCenterDistrict { get; set; }
        public string rightSegmentHighHouseNumber { get; set; }
        public string rightSegmentInterimAssistanceEligibilityIndicator { get; set; }
        public string rightSegmentLowHouseNumber { get; set; }
        public string rightSegmentNta { get; set; }
        public string rightSegmentNtaName { get; set; }
        public string rightSegmentPolicePatrolBoroughCommand { get; set; }
        public string rightSegmentPolicePrecinct { get; set; }
        public string rightSegmentZipCode { get; set; }
        public string roadwayType { get; set; }
        public string sanitationSnowPriorityCode { get; set; }
        public string secondStreetCode { get; set; }
        public string secondStreetNameNormalized { get; set; }
        public string segmentAzimuth { get; set; }
        public string segmentIdentifier { get; set; }
        public string segmentOrientation { get; set; }
        public string segmentTypeCode { get; set; }
        public string streetCode1 { get; set; }
        public string streetCode6 { get; set; }
        public string streetName1 { get; set; }
        public string streetName1In { get; set; }
        public string streetName2In { get; set; }
        public string streetName3In { get; set; }
        public string streetName6 { get; set; }
        public string streetStatus { get; set; }
        public string streetWidth { get; set; }
        public string thirdStreetCode { get; set; }
        public string thirdStreetNameNormalized { get; set; }
        public string toLgc1 { get; set; }
        public string toNode { get; set; }
        public string toXCoordinate { get; set; }
        public string toYCoordinate { get; set; }
        public string trafficDirection { get; set; }
        public string workAreaFormatIndicatorIn { get; set; }
    }

    public class BlockFaceResult
    {
        public BlockFace blockface { get; set; }
    }

    public class Address
    {
        public string assemblyDistrict { get; set; }
        public string bbl { get; set; }
        public string bblBoroughCode { get; set; }
        public string bblTaxBlock { get; set; }
        public string bblTaxLot { get; set; }
        public string boeLgcPointer { get; set; }
        public string boePreferredStreetName { get; set; }
        public string boePreferredstreetCode { get; set; }
        public string boroughCode1In { get; set; }
        public string buildingIdentificationNumber { get; set; }
        public string censusBlock2000 { get; set; }
        public string censusBlock2010 { get; set; }
        public string censusTract1990 { get; set; }
        public string censusTract2000 { get; set; }
        public string censusTract2010 { get; set; }
        public string cityCouncilDistrict { get; set; }
        public string civilCourtDistrict { get; set; }
        public string coincidenceSegmentCount { get; set; }
        public string communityDistrict { get; set; }
        public string communityDistrictBoroughCode { get; set; }
        public string communityDistrictNumber { get; set; }
        public string communitySchoolDistrict { get; set; }
        public string condominiumBillingBbl { get; set; }
        public string condominiumFlag { get; set; }
        public string congressionalDistrict { get; set; }
        public string cooperativeIdNumber { get; set; }
        public string crossStreetNamesFlagIn { get; set; }
        public string dcpPreferredLgc { get; set; }
        public string dofCondominiumIdentificationNumber { get; set; }
        public string dotStreetLightContractorArea { get; set; }
        public string dynamicBlock { get; set; }
        public string electionDistrict { get; set; }
        public string fireBattalion { get; set; }
        public string fireCompanyNumber { get; set; }
        public string fireCompanyType { get; set; }
        public string fireDivision { get; set; }
        public string firstBoroughName { get; set; }
        public string firstStreetCode { get; set; }
        public string firstStreetNameNormalized { get; set; }
        public string fromLionNodeId { get; set; }
        public string fromPreferredLgcsFirstSetOf5 { get; set; }
        public string genericId { get; set; }
        public string geosupportFunctionCode { get; set; }
        public string geosupportReturnCode { get; set; }
        public string geosupportReturnCode2 { get; set; }
        public string gi5DigitStreetCode1 { get; set; }
        public string giBoroughCode1 { get; set; }
        public string giBuildingIdentificationNumber1 { get; set; }
        public string giDcpPreferredLgc1 { get; set; }
        public string giHighHouseNumber1 { get; set; }
        public string giLowHouseNumber1 { get; set; }
        public string giSideOfStreetIndicator1 { get; set; }
        public string giStreetCode1 { get; set; }
        public string giStreetName1 { get; set; }
        public string healthArea { get; set; }
        public string healthCenterDistrict { get; set; }
        public string highBblOfThisBuildingsCondominiumUnits { get; set; }
        public string highCrossStreetB5SC1 { get; set; }
        public string highCrossStreetCode1 { get; set; }
        public string highCrossStreetName1 { get; set; }
        public string highHouseNumberOfBlockFaceSortFormat { get; set; }
        public string houseNumber { get; set; }
        public string houseNumberIn { get; set; }
        public string houseNumberSortFormat { get; set; }
        public string interimAssistanceEligibilityIndicator { get; set; }
        public string internalLabelXCoordinate { get; set; }
        public string internalLabelYCoordinate { get; set; }
        public string legacySegmentId { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string lionKeyBoroughCode { get; set; }
        public string lionKeyFaceCode { get; set; }
        public string lionKeyForVanityAddressBoroughCode { get; set; }
        public string lionKeyForVanityAddressFaceCode { get; set; }
        public string lionKeyForVanityAddressSequenceNumber { get; set; }
        public string lionKeySequenceNumber { get; set; }
        public string listOf4Lgcs { get; set; }
        public string lowBblOfThisBuildingsCondominiumUnits { get; set; }
        public string lowCrossStreetB5SC1 { get; set; }
        public string lowCrossStreetCode1 { get; set; }
        public string lowCrossStreetName1 { get; set; }
        public string lowHouseNumberOfBlockFaceSortFormat { get; set; }
        public string lowHouseNumberOfDefiningAddressRange { get; set; }
        public string nta { get; set; }
        public string ntaName { get; set; }
        public string numberOfCrossStreetB5SCsHighAddressEnd { get; set; }
        public string numberOfCrossStreetB5SCsLowAddressEnd { get; set; }
        public string numberOfCrossStreetsHighAddressEnd { get; set; }
        public string numberOfCrossStreetsLowAddressEnd { get; set; }
        public string numberOfEntriesInListOfGeographicIdentifiers { get; set; }
        public string numberOfExistingStructuresOnLot { get; set; }
        public string numberOfStreetFrontagesOfLot { get; set; }
        public string physicalId { get; set; }
        public string policePatrolBoroughCommand { get; set; }
        public string policePrecinct { get; set; }
        public string returnCode1a { get; set; }
        public string returnCode1e { get; set; }
        public string roadwayType { get; set; }
        public string rpadBuildingClassificationCode { get; set; }
        public string rpadSelfCheckCodeForBbl { get; set; }
        public string sanbornBoroughCode { get; set; }
        public string sanbornPageNumber { get; set; }
        public string sanbornVolumeNumber { get; set; }
        public string sanbornVolumeNumberSuffix { get; set; }
        public string sanitationCollectionSchedulingSectionAndSubsection { get; set; }
        public string sanitationDistrict { get; set; }
        public string sanitationRecyclingCollectionSchedule { get; set; }
        public string sanitationRegularCollectionSchedule { get; set; }
        public string sanitationSnowPriorityCode { get; set; }
        public string segmentAzimuth { get; set; }
        public string segmentIdentifier { get; set; }
        public string segmentLengthInFeet { get; set; }
        public string segmentOrientation { get; set; }
        public string segmentTypeCode { get; set; }
        public string selfCheckCodeOfBillingBbl { get; set; }
        public string sideOfStreetIndicator { get; set; }
        public string sideOfStreetOfVanityAddress { get; set; }
        public string splitLowHouseNumber { get; set; }
        public string stateSenatorialDistrict { get; set; }
        public string streetName1In { get; set; }
        public string streetStatus { get; set; }
        public string taxMapNumberSectionAndVolume { get; set; }
        public string toLionNodeId { get; set; }
        public string toPreferredLgcsFirstSetOf5 { get; set; }
        public string trafficDirection { get; set; }
        public string underlyingstreetCode { get; set; }
        public string workAreaFormatIndicatorIn { get; set; }
        public string xCoordinate { get; set; }
        public string xCoordinateHighAddressEnd { get; set; }
        public string xCoordinateLowAddressEnd { get; set; }
        public string xCoordinateOfCenterofCurvature { get; set; }
        public string yCoordinate { get; set; }
        public string yCoordinateHighAddressEnd { get; set; }
        public string yCoordinateLowAddressEnd { get; set; }
        public string yCoordinateOfCenterofCurvature { get; set; }
        public string zipCode { get; set; }
    }

    public class AddressResult
    {
        public Address address { get; set; }
    }

    public class Intersection
    {
        public string assemblyDistrict { get; set; }
        public string boroughCode1In { get; set; }
        public string censusTract1990 { get; set; }
        public string censusTract2000 { get; set; }
        public string censusTract2010 { get; set; }
        public string cityCouncilDistrict { get; set; }
        public string civilCourtDistrict { get; set; }
        public string communityDistrict { get; set; }
        public string communityDistrictBoroughCode { get; set; }
        public string communityDistrictNumber { get; set; }
        public string communitySchoolDistrict { get; set; }
        public string congressionalDistrict { get; set; }
        public string crossStreetNamesFlagIn { get; set; }
        public string dcpPreferredLgcForStreet1 { get; set; }
        public string dcpPreferredLgcForStreet2 { get; set; }
        public string dotStreetLightContractorArea { get; set; }
        public string fireBattalion { get; set; }
        public string fireCompanyNumber { get; set; }
        public string fireCompanyType { get; set; }
        public string fireDivision { get; set; }
        public string firstBoroughName { get; set; }
        public string firstStreetCode { get; set; }
        public string firstStreetNameNormalized { get; set; }
        public string geosupportFunctionCode { get; set; }
        public string geosupportReturnCode { get; set; }
        public string healthArea { get; set; }
        public string healthCenterDistrict { get; set; }
        public string interimAssistanceEligibilityIndicator { get; set; }
        public string intersectingStreet1 { get; set; }
        public string intersectingStreet2 { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string lionNodeNumber { get; set; }
        public string listOfPairsOfLevelCodes { get; set; }
        public string numberOfIntersectingStreets { get; set; }
        public string numberOfStreetCodesAndNamesInList { get; set; }
        public string policePatrolBoroughCommand { get; set; }
        public string policePrecinct { get; set; }
        public string sanbornBoroughCode1 { get; set; }
        public string sanbornBoroughCode2 { get; set; }
        public string sanbornPageNumber1 { get; set; }
        public string sanbornPageNumber2 { get; set; }
        public string sanbornVolumeNumber1 { get; set; }
        public string sanbornVolumeNumber2 { get; set; }
        public string sanbornVolumeNumberSuffix1 { get; set; }
        public string sanbornVolumeNumberSuffix2 { get; set; }
        public string sanitationCollectionSchedulingSectionAndSubsection { get; set; }
        public string sanitationDistrict { get; set; }
        public string secondStreetCode { get; set; }
        public string secondStreetNameNormalized { get; set; }
        public string stateSenatorialDistrict { get; set; }
        public string streetCode1 { get; set; }
        public string streetCode2 { get; set; }
        public string streetName1 { get; set; }
        public string streetName1In { get; set; }
        public string streetName2 { get; set; }
        public string streetName2In { get; set; }
        public string workAreaFormatIndicatorIn { get; set; }
        public string xCoordinate { get; set; }
        public string yCoordinate { get; set; }
        public string zipCode { get; set; }
    }

    public class IntersectionResult
    {
        public Intersection intersection { get; set; }
    }

    public class SpatialReference
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
    }

    public class Geometry
    {
        public double x { get; set; }
        public double y { get; set; }
    }

    public class GeometryResult
    {
        public string geometryType { get; set; }
        public List<Geometry> geometries { get; set; }
    }

    public class Candidate
    {
        public int score { get; set; }
        public string address { get; set; }
        public Geometry location { get; set; }
        public ExpandoObject attributes { get; set; }

        public Candidate()
        {
            location = new Geometry();
        }
    }

    public class GeocoderJsonResult
    {
        public SpatialReference spatialReference { get; set; }
        public List<Candidate> candidates { get; set; }
    }


    #region Added for Action - "find"
    public class Extent
    {
        public double xmin { get; set; }
        public double ymin { get; set; }
        public double xmax { get; set; }
        public double ymax { get; set; }
    }

    public class Feature
    {
        public Geometry geometry { get; set; }
        public ExpandoObject attributes { get; set; }
    }

    public class CandidateLocation
    {
        public string name { get; set; }
        public Extent extent { get; set; }
        public Feature feature { get; set; }
    }

    public class ExplorerJsonResult
    {
        public SpatialReference spatialReference { get; set; }
        public List<CandidateLocation> locations { get; set; }
    }
    #endregion
    #endregion

    #region Search Parameter Objects
    public class QueryParams
    {
        public string _rawQuery { get; set; }
        public string queryString { get; set; }
        public string callback { get; set; }
        public int outWkid { get; set; }
        public int outLatestWkid { get; set; }

        public string searchField { get; set; }
        public string searchURL { get; set; }
        public string searchFile { get; set; }

        public IGeoSearch searchObject { get; set; }
    }

    public interface IGeoSearch
    {
        string getParametersURL();
    }

    public class PlaceSearch : IGeoSearch
    {
        private string name { get; set; }
        private string borough { get; set; }
        private string zip { get; set; }

        public PlaceSearch(Dictionary<string, string> d)
        {
            this.name = "";
            this.zip = "";
            this.borough = d["BOROUGH"]; ;

            if (d.ContainsKey("NAME") && d["NAME"] != null) this.name = d["NAME"];
            if (d.ContainsKey("ZIP") && d["ZIP"] != null) this.zip = d["ZIP"];
        }

        public string getParametersURL()
        {
            string s = (zip == "") ?
                String.Format("name={0}&borough={1}", this.name, this.borough) :
                String.Format("name={0}&zip={1}", this.name, this.zip);

            return s;
        }
    }

    public class AddressSearch : IGeoSearch
    {
        private string houseNumber { get; set; }
        private string street { get; set; }
        private string borough { get; set; }
        private string zip { get; set; }

        public AddressSearch(Dictionary<string, string> d)
        {
            this.houseNumber = "";
            this.street = "";
            this.zip = "";
            this.borough = d["BOROUGH"];

            if (d.ContainsKey("HOUSENUMBER") && d["HOUSENUMBER"] != null) this.houseNumber = d["HOUSENUMBER"];
            if (d.ContainsKey("STREET") && d["STREET"] != null) this.street = d["STREET"];
            if (d.ContainsKey("ZIP") && d["ZIP"] != null) this.zip = d["ZIP"];
        }

        public string getParametersURL()
        {
            string s = (zip == "") ?
                String.Format("houseNumber={0}&street={1}&borough={2}", this.houseNumber, this.street, this.borough) :
                String.Format("houseNumber={0}&street={1}&zip={2}", this.houseNumber, this.street, this.zip);

            return s;
        }
    }

    public class BBLSearch : IGeoSearch
    {
        private string block { get; set; }
        private string lot { get; set; }
        private string borough { get; set; }

        public BBLSearch(Dictionary<string, string> d)
        {
            this.block = "";
            this.lot = "";
            this.borough = d["BOROUGH"]; ;

            if (d.ContainsKey("BLOCK") && d["BLOCK"] != null) this.block = d["BLOCK"];
            if (d.ContainsKey("LOT") && d["LOT"] != null) this.lot = d["LOT"];
        }

        public string getParametersURL()
        {
            string s = String.Format("block={0}&lot={1}&borough={2}", this.block, this.lot, this.borough);

            return s;
        }
    }

    public class BINSearch : IGeoSearch
    {
        private string bin { get; set; }

        public BINSearch(Dictionary<string, string> d)
        {
            this.bin = "";

            if (d.ContainsKey("BIN") && d["BIN"] != null) this.bin = d["BIN"];
        }

        public string getParametersURL()
        {
            string s = String.Format("bin={0}", this.bin);
            return s;
        }
    }

    public class BlockFaceSearch : IGeoSearch
    {
        private string onStreet { get; set; }
        private string crossStreetOne { get; set; }
        private string crossStreetTwo { get; set; }
        private string boroughCrossStreetOne { get; set; }
        private string boroughCrossStreetTwo { get; set; }
        private string compassDirection { get; set; }
        private string borough { get; set; }

        public BlockFaceSearch(Dictionary<string, string> d)
        {
            this.onStreet = "";
            this.crossStreetOne = "";
            this.crossStreetTwo = "";
            this.borough = d["BOROUGH"];

            if (d.ContainsKey("ONSTREET") && d["ONSTREET"] != null) this.onStreet = d["ONSTREET"];
            if (d.ContainsKey("CROSSSTREETONE") && d["CROSSSTREETONE"] != null) this.crossStreetOne = d["CROSSSTREETONE"];
            if (d.ContainsKey("CROSSSTREETTWO") && d["CROSSSTREETTWO"] != null) this.crossStreetTwo = d["CROSSSTREETTWO"];

            if (d.ContainsKey("BOROUGHCROSSSTREETONE") && d["BOROUGHCROSSSTREETONE"] != null) this.boroughCrossStreetOne = d["BOROUGHCROSSSTREETONE"];
            if (d.ContainsKey("BOROUGHCROSSSTREETTWO") && d["BOROUGHCROSSSTREETTWO"] != null) this.boroughCrossStreetTwo = d["BOROUGHCROSSSTREETTWO"];
            if (d.ContainsKey("COMPASSDIRECTION") && d["COMPASSDIRECTION"] != null) this.compassDirection = d["COMPASSDIRECTION"];
        }

        public string getParametersURL()
        {
            string s = String.Format("onStreet={0}&crossStreetOne={1}&crossStreetTwo={2}&borough={3}", this.onStreet, this.crossStreetOne, this.crossStreetTwo, this.borough);
            if (this.boroughCrossStreetOne != null) s += "&boroughCrossStreetOne=" + this.boroughCrossStreetOne;
            if (this.boroughCrossStreetTwo != null) s += "&boroughCrossStreetTwo=" + this.boroughCrossStreetTwo;
            if (this.compassDirection != null) s += "&compassDirection=" + this.compassDirection;

            return s;
        }
    }

    public class IntersectionSearch : IGeoSearch
    {
        private string crossStreetOne { get; set; }
        private string crossStreetTwo { get; set; }
        private string boroughCrossStreetTwo { get; set; }
        private string compassDirection { get; set; }
        private string borough { get; set; }

        public IntersectionSearch(Dictionary<string, string> d)
        {
            this.crossStreetOne = "";
            this.crossStreetTwo = "";
            this.borough = "";

            if (d.ContainsKey("CROSSSTREETONE") && d["CROSSSTREETONE"] != null) this.crossStreetOne = d["CROSSSTREETONE"];
            if (d.ContainsKey("CROSSSTREETTWO") && d["CROSSSTREETTWO"] != null) this.crossStreetTwo = d["CROSSSTREETTWO"];
            if (d.ContainsKey("BOROUGHCROSSSTREETTWO") && d["BOROUGHCROSSSTREETTWO"] != null) this.boroughCrossStreetTwo = d["BOROUGHCROSSSTREETTWO"];
            if (d.ContainsKey("COMPASSDIRECTION") && d["COMPASSDIRECTION"] != null) this.compassDirection = d["COMPASSDIRECTION"];
            if (d.ContainsKey("BOROUGH") && d["BOROUGH"] != null) this.borough = d["BOROUGH"];
        }

        public string getParametersURL()
        {
            string s = String.Format("crossStreetOne={0}&crossStreetTwo={1}&borough={2}", this.crossStreetOne, this.crossStreetTwo, this.borough);
            if (this.boroughCrossStreetTwo != null) s += "&boroughCrossStreetTwo=" + this.boroughCrossStreetTwo;
            if (this.compassDirection != null) s += "&compassDirection=" + this.compassDirection;

            return s;
        }
    }

    public class SingleInputSearch : IGeoSearch
    {
        private string input { get; set; }

        public SingleInputSearch(Dictionary<string, string> d)
        {
            this.input = "";

            if (d.ContainsKey("TEXT") && d["TEXT"] != null) this.input = d["TEXT"];
            if (d.ContainsKey("SINGLELINE") && d["SINGLELINE"] != null) this.input = d["SINGLELINE"];
        }

        public string getParametersURL()
        {
            string s = String.Format("input={0}", this.input);
            return s;
        }
    }
    #endregion

    static class Utils
    {
        public static Dictionary<string, string> toUpperKeyDictionary(this IEnumerable<KeyValuePair<string, string>> original)
        {
            Dictionary<string, string> clone = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in original)
            {
                clone.Add(kv.Key.ToUpper(), kv.Value);
            }

            return clone;
        }

        public static void CopyProperties(object source, ExpandoObject target)
        {
            IDictionary<string, object> kvPairs = target;
            var sourceProperties = source.GetType().GetProperties().Where(p => p.CanRead);

            foreach (var property in sourceProperties)
            {
                if (property.CanRead && !kvPairs.ContainsKey(property.Name))
                {
                    kvPairs.Add(property.Name, property.GetValue(source, null));
                }
            }
        }
    }
}

