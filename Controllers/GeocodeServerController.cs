using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Web.Http;
using System.Configuration;
using System.Dynamic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GeoREST.Controllers
{
    public class GeocodeServerController : ApiController
    {
        private string GeoClientAPIURL = "https://api.cityofnewyork.us/geoclient/v1/";
        private QueryParams queryParams = null;
        //private string format = "html";

        public GeocodeServerController()
        {
            this.queryParams = new QueryParams();
        }

        // GET metadata - api/GeocodeServer 
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
                   "<a href='GeocodeServer/geocodeAddresses'>Batch Geocoding</a><br/>" +
                   "<a href='GeocodeServer/find'>Search</a>";
        }

        [HttpGet]
        [ActionName("findAddressCandidates")]
        public Task<HttpResponseMessage> geocodeAddress()
        {
            return startGeocoding("findAddressCandidates");
        }

        [HttpGet]
        [ActionName("geocodeAddresses")]
        public Task<HttpResponseMessage> geocodeAddresses()
        {
            return startGeocoding("batch");
        }

        [HttpGet]
        [ActionName("find")]
        public Task<HttpResponseMessage> find()
        {
            return startGeocoding("find");
        }

        #region Start Geocoding
        public async Task<HttpResponseMessage> startGeocoding(string action)
        {
            var query = this.Request.GetQueryNameValuePairs();
            var q = this.Request.RequestUri;
            int paramCount = 0;

            #region Parse response format Parameter
            var matches = query.Where(kv => kv.Key.ToLower() == "f");
            if (matches.Count() > 0)
            {
                paramCount++;
            }
            #endregion

            #region Parse callback
            var matches1 = query.Where(kv => kv.Key.ToLower().IndexOf("callback") > -1);
            if (matches1.Count() > 0)
            {
                paramCount++;
                this.queryParams.callback = matches1.First().Value;
            }
            #endregion

            #region Parse outSR
            this.queryParams.outWkid = 4326;
            this.queryParams.outLatestWkid = 4326;

            var matches2 = query.Where(kv => kv.Key.ToUpper().IndexOf("OUTSR") > -1);
            if (matches2.Count() > 0)
            {
                paramCount++;
                string outSR = matches2.First().Value;
                int wkid = 0, latestWkid = 0;
                if (Int32.TryParse(outSR, out wkid))
                {
                    this.queryParams.outWkid = wkid;
                    this.queryParams.outLatestWkid = (wkid == 102100) ? 3857 : wkid;
                }
                else
                {
                    char[] separators = { ':', ',' };
                    string sr = outSR.Replace("{", "").Replace("}", "").Replace("\"", "").Replace("'", "");
                    string[] pairs = sr.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < pairs.Length; i++)
                    {
                        if (pairs[i] == "wkid")
                            wkid = Int32.Parse(pairs[i + 1]);
                        if (pairs[i] == "latestWkid")
                            latestWkid = Int32.Parse(pairs[i + 1]);
                    }

                    this.queryParams.outWkid = (wkid > 0) ? wkid : ((latestWkid > 0) ? latestWkid : 4326);
                    this.queryParams.outLatestWkid = (latestWkid > 0) ? latestWkid : this.queryParams.outWkid;
                }
            }
            #endregion

            if (query.Count() <= paramCount)
            {
                string errMsg = "{error: {status:\"rejected\", message: \"Sorry, no valid address parameters were found in the query.\"}}";

                if (this.queryParams.callback != null) errMsg = this.queryParams.callback + "(" + errMsg + ");";
                var responseMsg = new HttpResponseMessage(HttpStatusCode.BadRequest);
                responseMsg.Content = new StringContent(errMsg, System.Text.Encoding.UTF8, "text/json");
                return responseMsg;
            }
            else if (action == "batch")
            {
                BatchAddresses batchAddresses = null;
                var matches3 = query.Where(kv => kv.Key.ToUpper().IndexOf("ADDRESSES") > -1);
                if (matches3.Count() > 0)
                {
                    string jsonAddresses = matches3.First().Value.ToLower();
                    if (jsonAddresses != "")
                    {
                        using (MemoryStream jsonStream = new MemoryStream(Encoding.Unicode.GetBytes(jsonAddresses)))
                        {
                            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(BatchAddresses));
                            batchAddresses = (BatchAddresses)serializer.ReadObject(jsonStream);
                        }
                    }
                }

                return await batchGeocoding(action, batchAddresses);
            }
            else
            {
                Dictionary<string, string> d = query.toUpperKeyDictionary();
                SearchParams searchParams = parseSearchParams(d);
                object candidate = sendRequest(action, searchParams);

                if (candidate is Candidate)
                    return await serializeCandidates(candidate as Candidate);
                else
                    return await serializeFindResult(candidate as CandidateLocation);
            }
        }

        public Task<HttpResponseMessage> batchGeocoding(string action, BatchAddresses addresses)
        {
            string jsonResult = "";

            SpatialReference spatialReference = new SpatialReference();
            spatialReference.wkid = this.queryParams.outWkid;
            spatialReference.latestWkid = this.queryParams.outLatestWkid;

            BatchGeocoderJsonResult bacthResult = new BatchGeocoderJsonResult();
            bacthResult.spatialReference = spatialReference;
            bacthResult.locations = new List<Candidate>();

            if (addresses != null)
            {
                foreach (BatchRecord record in addresses.records)
                {
                    Dictionary<string, string> d = record.attributes.toUpperKeyDictionary();
                    SearchParams searchParams = parseSearchParams(d);
                    searchParams.objectID = d.ContainsKey("OBJECTID") ? d["OBJECTID"] : "0";

                    object candidate = sendRequest(action, searchParams);
                    bacthResult.locations.Add(candidate as Candidate);
                }

                jsonResult = JsonConvert.SerializeObject(bacthResult);
            }
            else
            {
                jsonResult = "{error: {status:\"rejected\", message: \"Sorry, no address entries were found in the [addresses] query parameter.\"}}";
            }

            if (this.queryParams.callback != null) jsonResult = this.queryParams.callback + "(" + jsonResult + ");";
            var responseMsg = new HttpResponseMessage(HttpStatusCode.OK);
            responseMsg.Content = new StringContent(jsonResult, System.Text.Encoding.UTF8, "text/json");

            return Task.FromResult(responseMsg);
        }
 
        private SearchParams parseSearchParams(Dictionary<string, string> d)
        {
            SearchParams searchParams = new SearchParams() { searchURL = "" };

            // Add MANHATTAN as default borough
            if (!d.ContainsKey("BOROUGH")) d.Add("BOROUGH", "Manhattan");

            if (d.ContainsKey("NAME"))
            {
                searchParams.searchObject = new PlaceSearch(d);
                searchParams.searchFile = "place.json";
            }
            else if (d.ContainsKey("HOUSENUMBER"))
            {
                searchParams.searchObject = new AddressSearch(d);
                searchParams.searchFile = "address.json";
            }
            else if (d.ContainsKey("LOT"))
            {
                searchParams.searchObject = new BBLSearch(d);
                searchParams.searchFile = "bbl.json";
            }
            else if (d.ContainsKey("BIN"))
            {
                searchParams.searchObject = new BINSearch(d);
                searchParams.searchFile = "bin.json";
            }
            else if (d.ContainsKey("ONSTREET"))
            {
                searchParams.searchObject = new BlockFaceSearch(d);
                searchParams.searchFile = "blockface.json";
            }
            else if (d.ContainsKey("CROSSSTREETTWO") && !d.ContainsKey("ONSTREET"))
            {
                searchParams.searchObject = new IntersectionSearch(d);
                searchParams.searchFile = "intersection.json";
            }
            else if (d.ContainsKey("SINGLELINE"))
            {
                searchParams.searchObject = new SingleInputSearch(d);
                searchParams.searchFile = "search.json";
            }
            else if (d.ContainsKey("TEXT"))
            {
                searchParams.searchObject = new SingleInputSearch(d);
                searchParams.searchFile = "search.json";
            }
            else
            {
                searchParams._rawQuery = this.Request.RequestUri.Query.Remove(0, 1).Replace("\n", " ");
                searchParams.searchObject = new SingleInputSearch(d);
                searchParams.searchFile = "search.json";
                searchParams.searchURL = String.Format(this.GeoClientAPIURL + "{0}?input={1}", searchParams.searchFile, searchParams._rawQuery);
            }

            if (searchParams.searchURL == "")
            {
                searchParams.searchURL = String.Format(this.GeoClientAPIURL + "{0}?{1}", searchParams.searchFile, searchParams.searchObject.getParametersURL());
            }

            return searchParams;
        }
        #endregion

        #region Send Search Request to GeoSupport Geocoder
        private object sendRequest(string action, SearchParams searchParams)
        {
            object result = null;

            if (!string.IsNullOrEmpty(searchParams.searchURL))
            {
                var appSettings = ConfigurationManager.AppSettings;
                string sAuth = string.Format("&app_id={0}&app_key={1}", appSettings["app_id"], appSettings["app_key"]);
                HttpWebRequest request = WebRequest.CreateHttp(searchParams.searchURL + sAuth);
                //RequestState myRequestState = new RequestState();
                // The 'WebRequest' object is associated 
                //request.BeginGetResponse()
                using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                {
                    Stream responseStream = copyStream(response.GetResponseStream());

                    if (searchParams.searchObject.GetType() == typeof(SingleInputSearch))
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
                                result = getLocationCandidate(place, searchParams.objectID, action);
                            }
                            else if (requestMatch.StartsWith("BBL"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseBBL));
                                SearchResponseBBL response2 = (SearchResponseBBL)serializer2.ReadObject(responseStream);
                                BBL bbl = response2.results[0].response;
                                result = getLocationCandidate(bbl, searchParams.objectID, action);
                            }
                            else if (requestMatch.StartsWith("BIN"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseBIN));
                                SearchResponseBIN response2 = (SearchResponseBIN)serializer2.ReadObject(responseStream);
                                BIN bin = response2.results[0].response;
                                result = getLocationCandidate(bin, searchParams.objectID, action);
                            }
                            else if (requestMatch.StartsWith("BLOCKFACE"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseBlockFace));
                                SearchResponseBlockFace response2 = (SearchResponseBlockFace)serializer2.ReadObject(responseStream);
                                BlockFace blockface = response2.results[0].response;
                                result = getLocationCandidate(blockface, searchParams.objectID, action);
                            }
                            else if (requestMatch.StartsWith("INTERSECTION"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseIntersection));
                                SearchResponseIntersection response2 = (SearchResponseIntersection)serializer2.ReadObject(responseStream);
                                Intersection intersect = response2.results[0].response;
                                result = getLocationCandidate(intersect, searchParams.objectID, action);
                            }
                            else if (requestMatch.StartsWith("ADDRESS"))
                            {
                                DataContractJsonSerializer serializer2 = new DataContractJsonSerializer(typeof(SearchResponseAddress));
                                SearchResponseAddress response2 = (SearchResponseAddress)serializer2.ReadObject(responseStream);
                                Address address = response2.results[0].response;
                                result = getLocationCandidate(address, searchParams.objectID, action);
                            }
                        }
                    }
                    else if (searchParams.searchObject.GetType() == typeof(PlaceSearch))
                    {
                        DataContractJsonSerializer placeSerializer = new DataContractJsonSerializer(typeof(PlaceResult));
                        PlaceResult placeResult = (PlaceResult)placeSerializer.ReadObject(responseStream);
                        result = getLocationCandidate(placeResult.place, searchParams.objectID, action);
                    }
                    else if (searchParams.searchObject.GetType() == typeof(BBLSearch))
                    {
                        DataContractJsonSerializer BBLSerializer = new DataContractJsonSerializer(typeof(BBLResult));
                        BBLResult bblResult = (BBLResult)BBLSerializer.ReadObject(responseStream);
                        result = getLocationCandidate(bblResult.bbl, searchParams.objectID, action);
                    }
                    else if (searchParams.searchObject.GetType() == typeof(BINSearch))
                    {
                        DataContractJsonSerializer BinSerializer = new DataContractJsonSerializer(typeof(BINResult));
                        BINResult binResult = (BINResult)BinSerializer.ReadObject(responseStream);
                        result = getLocationCandidate(binResult.bin, searchParams.objectID, action);
                    }
                    else if (searchParams.searchObject.GetType() == typeof(BlockFaceSearch))
                    {
                        DataContractJsonSerializer BlockFaceSerializer = new DataContractJsonSerializer(typeof(BlockFaceResult));
                        BlockFaceResult blockFaceResult = (BlockFaceResult)BlockFaceSerializer.ReadObject(responseStream);
                        result = getLocationCandidate(blockFaceResult.blockface, searchParams.objectID, action);
                    }
                    else if (searchParams.searchObject.GetType() == typeof(IntersectionSearch))
                    {
                        DataContractJsonSerializer IntersectionSerializer = new DataContractJsonSerializer(typeof(IntersectionResult));
                        IntersectionResult intersectionResult = (IntersectionResult)IntersectionSerializer.ReadObject(responseStream);
                        result = getLocationCandidate(intersectionResult.intersection, searchParams.objectID, action);
                    }
                    else if (searchParams.searchObject.GetType() == typeof(AddressSearch))
                    {
                        DataContractJsonSerializer addressSerializer = new DataContractJsonSerializer(typeof(AddressResult));
                        AddressResult addressResult = (AddressResult)addressSerializer.ReadObject(responseStream);
                        result = getLocationCandidate(addressResult.address, searchParams.objectID, action);
                    }
                    else
                    {
                        StreamReader reader = new StreamReader(responseStream);
                        result = reader.ReadToEnd();
                    }
                }
            }

            return result;
        }
 
        // Return Candidate or LocationCandidate
        private object getLocationCandidate(object result, string objectID, string action)
        { 
            double lat = 0, lon = 0;
            string address = "", locType = "GeoClient";

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
                return null;
            }
            else if (action == "find")
            {
                CandidateLocation candidate1 = new CandidateLocation();
                candidate1.name = address;
                candidate1.extent = new Extent();
                candidate1.feature = new Feature();

                //get WebMercator
                Geometry geo = ensureWebMeractor(lon, lat);
                candidate1.feature.geometry = geo;

                candidate1.extent.xmin = (this.queryParams.outWkid == 4326) ? (geo.x - 0.001) : (geo.x - 110);
                candidate1.extent.xmax = (this.queryParams.outWkid == 4326) ? (geo.x + 0.001) : (geo.x + 110);
                candidate1.extent.ymin = (this.queryParams.outWkid == 4326) ? (geo.y - 0.001) : (geo.y - 130);
                candidate1.extent.ymax = (this.queryParams.outWkid == 4326) ? (geo.y + 0.001) : (geo.y + 130);

                dynamic attr = new ExpandoObject();
                attr.score = 100;
                attr.Addr_type = locType;
                Utils.CopyProperties(result, attr);
                candidate1.feature.attributes = attr;

                return candidate1;
            }
            else
            {
                Candidate candidate = new Candidate();
                candidate.score = 100;
                candidate.address = address;

                dynamic attributes = new ExpandoObject();
                attributes.Loc_name = locType;

                if (action == "batch")
                {
                    attributes.Match_addr = address;
                    attributes.OBJECTID = Int32.Parse(objectID);
                }

                Utils.CopyProperties(result, attributes);
                candidate.attributes = attributes;
                candidate.location = ensureWebMeractor(lon, lat);
      
                return candidate;
            }
        }
        #endregion

        #region serialize results into JSON - for "findAddressCandidates" operation
        private Task<HttpResponseMessage> serializeCandidates(object candidate)
        {
            string jsonResult = "";

            if (candidate == null)
            {
                jsonResult = "{error: {status:\"failed\", message: \"GeoClient does not return geographic coordinates for this search\"}}";
            }
            else if (candidate is string)
            {
                jsonResult = candidate as string;
            }
            else if (candidate is Candidate)
            {
                SpatialReference spatialReference = new SpatialReference();
                spatialReference.wkid = this.queryParams.outWkid;
                spatialReference.latestWkid = this.queryParams.outLatestWkid;

                ArcGISGeocoderJsonResult geoResult = new ArcGISGeocoderJsonResult();
                geoResult.spatialReference = spatialReference;
                geoResult.candidates = new List<Candidate>();
                geoResult.candidates.Add(candidate as Candidate);

                //MemoryStream mstream = new MemoryStream();
                //DataContractJsonSerializer ser2 = new DataContractJsonSerializer(geoResult.GetType());
                //ser2.WriteObject(mstream, geoResult);

                //mstream.Position = 0;
                //StreamReader sr = new StreamReader(mstream);
                //jsonResult = sr.ReadToEnd();
                jsonResult = JsonConvert.SerializeObject(geoResult);
            }

            if (this.queryParams.callback != null) jsonResult = this.queryParams.callback + "(" + jsonResult + ");";
            var responseMsg = new HttpResponseMessage(HttpStatusCode.OK);

            responseMsg.Content = new StringContent(jsonResult, System.Text.Encoding.UTF8, "text/json");
            return Task.FromResult(responseMsg);
        }
        #endregion

        #region serialize results into JSON - for "find" operation
        private Task<HttpResponseMessage> serializeFindResult(object candidate)
        {
            string jsonResult = "";

            if (candidate == null)
            {
                jsonResult = "{error: {status:\"failed\", message: \"GeoClient does not return geographic coordinates for this search\"}}";
            }
            else if (candidate is string)
            {
                jsonResult = candidate as string;
            }
            else if (candidate is CandidateLocation)
            {
                ExplorerJsonResult fResult = new ExplorerJsonResult();
                fResult.spatialReference = new SpatialReference();
                fResult.spatialReference.wkid = this.queryParams.outWkid;
                fResult.spatialReference.latestWkid = this.queryParams.outLatestWkid;

                fResult.locations = new List<CandidateLocation>();
                fResult.locations.Add(candidate as CandidateLocation);

                //MemoryStream mstream = new MemoryStream();
                //DataContractJsonSerializer ser2 = new DataContractJsonSerializer(typeof(ExplorerJsonResult));
                //ser2.WriteObject(mstream, fResult);

                //mstream.Position = 0;
                //StreamReader sr = new StreamReader(mstream);
                //jsonResult = sr.ReadToEnd();
                jsonResult = JsonConvert.SerializeObject(fResult);
            }

            if (this.queryParams.callback != null) jsonResult = this.queryParams.callback + "(" + jsonResult + ");";
            var responseMsg = new HttpResponseMessage(HttpStatusCode.OK);

            responseMsg.Content = new StringContent(jsonResult, System.Text.Encoding.UTF8, "text/json");
            return Task.FromResult(responseMsg);
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

        /* Unused
        private Dictionary<string, string> doParse(string s)
        {
            Dictionary<string, string> d = new Dictionary<string, string>();

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

            return d;
        }
        */

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

    #region Batch Geocoding Input Object
    public class BatchAddresses
    {
        public BatchRecord[] records { get; set; }
    }

    public class BatchRecord
    {
        public BatchAttributes attributes { get; set; }
    }

    public class BatchAttributes
    {
        public int objectid { get; set; }
        public string name { get; set; }
        public string housenumber { get; set; }
        public string street { get; set; }
        public string borough { get; set; }
        public string zip { get; set; }
        public string block { get; set; }
        public string lot { get; set; }
        public string bin { get; set; }
        public string onstreet { get; set; }
        public string crossstreetone { get; set; }
        public string crossstreettwo { get; set; }
        public string boroughcrossstreetone { get; set; }
        public string boroughcrossstreettwo { get; set; }
        public string compassdirection { get; set; }
        public string singleline { get; set; }
    }

    public class BatchGeocoderJsonResult
    {
        public SpatialReference spatialReference { get; set; }
        public List<Candidate> locations { get; set; }
    }
    #endregion

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

    #region GeoSupport Geocoding Result Objects
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
    #endregion

    #region ArcGIS Locator Result Objects for Serialization
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

    public class ArcGISGeocoderJsonResult
    {
        public SpatialReference spatialReference { get; set; }
        public List<Candidate> candidates { get; set; }
    }
    #endregion

    #region Result classes for "find" operation
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

    #region Search Parameter Objects
    public class QueryParams
    {
        public string callback { get; set; }
        public int outWkid { get; set; }
        public int outLatestWkid { get; set; }
    }

    public class SearchParams
    {
        public string _rawQuery { get; set; }
        public string objectID { get; set; }
        public string searchURL { get; set; }
        public string searchFile { get; set; }
        public string searchField { get; set; }

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

            if (d.ContainsKey("NAME")) this.name = d["NAME"];
            if (d.ContainsKey("ZIP")) this.zip = d["ZIP"];
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

            if (d.ContainsKey("HOUSENUMBER")) this.houseNumber = d["HOUSENUMBER"];
            if (d.ContainsKey("STREET")) this.street = d["STREET"];
            if (d.ContainsKey("ZIP")) this.zip = d["ZIP"];
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

            if (d.ContainsKey("BLOCK")) this.block = d["BLOCK"];
            if (d.ContainsKey("LOT")) this.lot = d["LOT"];
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

            if (d.ContainsKey("BIN") ) this.bin = d["BIN"];
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

            if (d.ContainsKey("ONSTREET")) this.onStreet = d["ONSTREET"];
            if (d.ContainsKey("CROSSSTREETONE")) this.crossStreetOne = d["CROSSSTREETONE"];
            if (d.ContainsKey("CROSSSTREETTWO")) this.crossStreetTwo = d["CROSSSTREETTWO"];

            if (d.ContainsKey("BOROUGHCROSSSTREETONE")) this.boroughCrossStreetOne = d["BOROUGHCROSSSTREETONE"];
            if (d.ContainsKey("BOROUGHCROSSSTREETTWO")) this.boroughCrossStreetTwo = d["BOROUGHCROSSSTREETTWO"];
            if (d.ContainsKey("COMPASSDIRECTION")) this.compassDirection = d["COMPASSDIRECTION"];
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

            if (d.ContainsKey("CROSSSTREETONE")) this.crossStreetOne = d["CROSSSTREETONE"];
            if (d.ContainsKey("CROSSSTREETTWO")) this.crossStreetTwo = d["CROSSSTREETTWO"];
            if (d.ContainsKey("BOROUGHCROSSSTREETTWO")) this.boroughCrossStreetTwo = d["BOROUGHCROSSSTREETTWO"];
            if (d.ContainsKey("COMPASSDIRECTION")) this.compassDirection = d["COMPASSDIRECTION"];
            if (d.ContainsKey("BOROUGH")) this.borough = d["BOROUGH"];
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

            if (d.ContainsKey("TEXT")) this.input = d["TEXT"];
            if (d.ContainsKey("SINGLELINE")) this.input = d["SINGLELINE"];
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
        public static Dictionary<string, string> toUpperKeyDictionary(this BatchAttributes original)
        {
            Dictionary<string, string> clone = new Dictionary<string, string>();
            foreach (PropertyInfo property in original.GetType().GetProperties())
            {
                var value = property.GetValue(original);
                if (value != null)
                {
                    clone.Add(property.Name.ToUpper(), value.ToString());
                }
            }

            return clone;
        }

        public static Dictionary<string, string> toUpperKeyDictionary(this IEnumerable<KeyValuePair<string, string>> original)
        {
            Dictionary<string, string> clone = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in original)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    clone.Add(kv.Key.ToUpper(), kv.Value);
                }
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

