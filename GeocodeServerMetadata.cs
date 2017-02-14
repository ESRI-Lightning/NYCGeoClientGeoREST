using Newtonsoft.Json;
using System.Collections.Generic;

namespace GeoREST
{
    public class SingleLineAddressField
    {
        public string name { get; set; }
        public string type { get; set; }
        public string alias { get; set; }
        public bool required { get; set; }
        public int length { get; set; }
        public LocalizedNames localizedNames { get; set; }
        public RecognizedNames recognizedNames { get; set; }
    }

    public class AddressField
    {
        public string name { get; set; }
        public string type { get; set; }
        public string alias { get; set; }
        public bool required { get; set; }
        public int length { get; set; }
        public LocalizedNames localizedNames { get; set; }
        public RecognizedNames recognizedNames { get; set; }
    }

    public class CandidateField
    {
        public string name { get; set; }
        public string type { get; set; }
        public string alias { get; set; }
        public bool required { get; set; }
        public int length { get; set; }
    }

    public class SpatialReference
    {
        public int wkid { get; set; }
        public int latestWkid { get; set; }
    }

    public class AddressCategory
    {
        public string name { get; set; }
        public LocalizedNames localizedNames { get; set; }
        public List<AddressCategory> categories { get; set; }
    }

    //public class AddressCategoryLocalizedNames
    //{
    //    public string name { get; set; }
    //    public LocalizedNames localizedNames { get; set; }
    //}

    public class LocatorProperties
    {
        public string UICLSID { get; set; }
        public string IntersectionConnectors { get; set; }
        public int SuggestedBatchSize { get; set; }
        public int MaxBatchSize { get; set; }
        public int LoadBalancerTimeOut { get; set; }
        public string WriteXYCoordFields { get; set; }
        public string WriteStandardizedAddressField { get; set; }
        public string WriteReferenceIDField { get; set; }
        public string WritePercentAlongField { get; set; }
    }

    public class LocalizedNames
    {
        public string en { get; set; }

        [JsonProperty(PropertyName = "en-us")]
        public string en_us { get; set; }

        [JsonProperty(PropertyName = "en-vi")]
        public string en_vi { get; set; }
    }

    public class RecognizedNames
    {
        public string[] en { get; set; }

        [JsonProperty(PropertyName = "en-us")]
        public string[] en_us { get; set; }

        [JsonProperty(PropertyName = "en-vi")]
        public string[] en_vi { get; set; }
    }

    public class GeocodeServerMetadata
    {
        public double currentVersion { get; set; }
        public string serviceDescription { get; set; }
        public List<AddressField> addressFields { get; set; }
        public List<AddressCategory> categories { get; set; }
        public SingleLineAddressField singleLineAddressField { get; set; }
        public List<CandidateField> candidateFields { get; set; }
        public SpatialReference spatialReference { get; set; }
        public LocatorProperties locatorProperties { get; set; }
        public List<string> countries { get; set; }
        public string capabilities { get; set; }

        public GeocodeServerMetadata()
        {
            this.currentVersion = 10.41;
            this.serviceDescription = "New York City";
      
            this.singleLineAddressField = new SingleLineAddressField()
            {
                name = "SingleLine",
                type = "esriFieldTypeString",
                alias = "Single Line Input",
                required = false,
                length = 200,
                localizedNames = new LocalizedNames() { en = "Single Line Input", en_us = "Single Line Input", en_vi = "Single Line Input" },
                recognizedNames = new RecognizedNames() { en = new string[2] { "FullAddress", "SingleLine" }, en_us = new string[2] { "FullAddress", "SingleLine" }, en_vi = new string[2] { "FullAddress", "SingleLine" } }
            };

            this.categories = new List<AddressCategory>();
            AddressCategory category1 = new AddressCategory()
            {
                name = "Address",
                localizedNames = new LocalizedNames() { en = "Address", en_us = "Address", en_vi = "Address" },
                categories = new List<AddressCategory>()
            };
            category1.categories.Add(new AddressCategory() { name = "Point Address" });
            category1.categories.Add(new AddressCategory() { name = "Street Address" });
            category1.categories.Add(new AddressCategory() { name = "Street Name" });
            this.categories.Add(category1);

            AddressCategory category2 = new AddressCategory()
            {
                name = "Postal",
                localizedNames = new LocalizedNames() { en = "Postal", en_us = "Postal", en_vi = "Postal" },
                categories = new List<AddressCategory>()
            };
            category2.categories.Add(new AddressCategory() { name = "Primary Postal" });
            category2.categories.Add(new AddressCategory() { name = "Postal Locality" });
            category2.categories.Add(new AddressCategory() { name = "Postal Extension" });
            this.categories.Add(category2);

            AddressCategory category3 = new AddressCategory()
            {
                name = "Coordinate System",
                localizedNames = new LocalizedNames() { en = "Coordinate System", en_us = "Coordinate System", en_vi = "Coordinate System" },
                categories = new List<AddressCategory>()
            };
            category3.categories.Add(new AddressCategory() { name = "LatLong" });
            category3.categories.Add(new AddressCategory() { name = "XY" });
            category3.categories.Add(new AddressCategory() { name = "X-Y" });
            this.categories.Add(category3);

            AddressCategory category4 = new AddressCategory()
            {
                name = "POI",
                localizedNames = new LocalizedNames() { en = "POI", en_us = "POI", en_vi = "POI" },
                categories = new List<AddressCategory>()
            };
            this.categories.Add(category4);

            AddressCategory poiCategory1 = new AddressCategory()
            {
                name = "Arts and Entertainment",
                localizedNames = new LocalizedNames() { en = "Arts and Entertainment", en_us = "Arts and Entertainment", en_vi = "Arts and Entertainment" },
                categories = new List<AddressCategory>()
            };
            poiCategory1.categories.Add(new AddressCategory() { name = "Amusement Park" });
            poiCategory1.categories.Add(new AddressCategory() { name = "Aquarium" });
            poiCategory1.categories.Add(new AddressCategory() { name = "Art Gallery" });
            poiCategory1.categories.Add(new AddressCategory() { name = "Art Museum" });
            poiCategory1.categories.Add(new AddressCategory() { name = "Museum" });
            poiCategory1.categories.Add(new AddressCategory() { name = "Casino" });
            poiCategory1.categories.Add(new AddressCategory() { name = "Cinema" });
            poiCategory1.categories.Add(new AddressCategory() { name = "History Museum" });
            category4.categories.Add(poiCategory1);

            AddressCategory poiCategory2 = new AddressCategory()
            {
                name = "Education",
                localizedNames = new LocalizedNames() { en = "Education", en_us = "Education", en_vi = "Education" },
                categories = new List<AddressCategory>()
            };
            poiCategory2.categories.Add(new AddressCategory() { name = "College" });
            poiCategory2.categories.Add(new AddressCategory() { name = "School" });
            category4.categories.Add(poiCategory2);

            AddressCategory poiCategory3 = new AddressCategory()
            {
                name = "Food",
                localizedNames = new LocalizedNames() { en = "Food", en_us = "Food", en_vi = "Food" },
                categories = new List<AddressCategory>()
            };
            poiCategory3.categories.Add(new AddressCategory() { name = "American Food" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Bakery" });
            poiCategory3.categories.Add(new AddressCategory() { name = "BBQ and Southern Food" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Bistro" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Burgers" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Chinese Food" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Coffee Shop" });
            poiCategory3.categories.Add(new AddressCategory() { name = "French Food" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Japanese Food" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Ice Cream" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Mexican Food" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Pizza" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Seafood" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Steak House" });
            poiCategory3.categories.Add(new AddressCategory() { name = "Sushi" });
            category4.categories.Add(poiCategory3);

            AddressCategory poiCategory4 = new AddressCategory()
            {
                name = "Nightlife Spot",
                localizedNames = new LocalizedNames() { en = "Nightlife Spot", en_us = "Nightlife Spot", en_vi = "Nightlife Spot" },
                categories = new List<AddressCategory>()
            };
            poiCategory4.categories.Add(new AddressCategory() { name = "Bar or Pub" });
            poiCategory4.categories.Add(new AddressCategory() { name = "Dancing" });
            poiCategory4.categories.Add(new AddressCategory() { name = "Night Club" });
            category4.categories.Add(poiCategory4);

            AddressCategory poiCategory5 = new AddressCategory()
            {
                name = "Parks and Outdoors",
                localizedNames = new LocalizedNames() { en = "Parks and Outdoors", en_us = "Parks and Outdoors", en_vi = "Parks and Outdoors" },
                categories = new List<AddressCategory>()
            };
            poiCategory5.categories.Add(new AddressCategory() { name = "Basketball" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Fishing" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Garden" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Golf Course" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Hockey" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Ice Skating" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Park" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Soccer" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Swimming Pool" });
            poiCategory5.categories.Add(new AddressCategory() { name = "Tennis Court" });
            category4.categories.Add(poiCategory5);

            this.spatialReference = new SpatialReference() { wkid = 4326, latestWkid = 4326 };
            this.countries = new List<string>();
            this.countries.Add("US");

            this.addressFields = new List<AddressField>();
            this.addressFields.Add(new AddressField()
            {
                alias = "Place",
                required = false,
                length = 100,
                name = "Place",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "Place", en_us = "Place", en_vi = "Place" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[2] { "Place", "Place Name" },
                    en_us = new string[2] { "Place", "Place Name" },
                    en_vi = new string[2] { "Place", "Place Name" }
                }
            });

            this.addressFields.Add(new AddressField()
            {
                alias = "Address",
                required = false,
                length = 100,
                name = "Address",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "Address", en_us = "Address", en_vi = "Address" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[3] { "Address", "Street", "Street Address" },
                    en_us = new string[3] { "Address", "Street", "Street Address" },
                    en_vi = new string[3] { "Address", "Street", "Street Address" }
                }
            });

            this.addressFields.Add(new AddressField()
            {
                alias = "Borough",
                required = false,
                length = 16,
                name = "Borough",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "Borough", en_us = "Borough", en_vi = "Borough" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[3] { "Borough", "Boro", "City" },
                    en_us = new string[3] { "Borough", "Boro", "City" },
                    en_vi = new string[3] { "Borough", "Boro", "City" }
                }
            });

            this.addressFields.Add(new AddressField()
            {
                alias = "BBL",
                required = false,
                length = 20,
                name = "BBL",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "BBL", en_us = "BBL", en_vi = "BBL" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[4] { "Borough Block Lot", "Parcel Number", "Parcel No", "PIN" },
                    en_us = new string[4] { "Borough Block Lot", "Parcel Number", "Parcel No", "PIN" },
                    en_vi = new string[4] { "Borough Block Lot", "Parcel Number", "Parcel No", "PIN" }
                }
            });

            this.addressFields.Add(new AddressField()
            {
                alias = "BIN",
                required = false,
                length = 20,
                name = "BIN",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "BIN", en_us = "BIN", en_vi = "BIN" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[3] { "Building Identification Number", "Building Number", "Building ID" },
                    en_us = new string[3] { "Building Identification Number", "Building Number", "Building ID" },
                    en_vi = new string[3] { "Building Identification Number", "Building Number", "Building ID" }
                }
            });
 
            this.addressFields.Add(new AddressField()
            {
                alias = "BlockFace",
                required = false,
                length = 40,
                name = "BlockFace",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "BlockFace", en_us = "BlockFace", en_vi = "BlockFace" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[2] { "Block Face", "Block Face Code" },
                    en_us = new string[2] { "Block Face", "Block Face Code" },
                    en_vi = new string[2] { "Block Face", "Block Face Code" }
                }
            });

            this.addressFields.Add(new AddressField()
            {
                alias = "Intersection",
                required = false,
                length = 100,
                name = "Intersection",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "Intersection", en_us = "Intersection", en_vi = "Intersection" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[2] { "Intersection", "Cross Street" },
                    en_us = new string[2] { "Intersection", "Cross Street" },
                    en_vi = new string[2] { "Intersection", "Cross Street" }
                }
            });

            this.addressFields.Add(new AddressField()
            {
                alias = "Postal",
                required = false,
                length = 20,
                name = "Postal",
                type = "esriFieldTypeString",
                localizedNames = new LocalizedNames() { en = "Postal", en_us = "Postal", en_vi = "Postal" },
                recognizedNames = new RecognizedNames()
                {
                    en = new string[6] { "Postal", "Postal Code", "PostalCode", "ZIP", "ZIP5", "Zipcode" },
                    en_us = new string[6] { "Postal", "Postal Code", "PostalCode", "ZIP", "ZIP5", "Zipcode" },
                    en_vi = new string[6] { "Postal", "Postal Code", "PostalCode", "ZIP", "ZIP5", "Zipcode" }
                }
            });

            this.candidateFields = new List<CandidateField>();
            this.candidateFields.Add(new CandidateField() { name = "Loc_name", type = "esrifieldTypeString", alias = "Loc_name", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "Match_addr", type = "esrifieldTypeString", alias = "Match_addr", required = false, length = 50 });
            this.candidateFields.Add(new CandidateField() { name = "Addr_type", type = "esrifieldTypeString", alias = " Addr_type", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "bbl", type = "esrifieldTypeString", alias = "bbl", required = false, length = 20 });
            this.candidateFields.Add(new CandidateField() { name = "bblTaxBlock", type = "esrifieldTypeString", alias = "bblTaxBlock", required = false, length = 10 });
            this.candidateFields.Add(new CandidateField() { name = "bblTaxLot", type = "esrifieldTypeString", alias = "bblTaxLot", required = false, length = 10 });
            this.candidateFields.Add(new CandidateField() { name = "bblBoroughCode", type = "esrifieldTypeString", alias = "bblBoroughCode", required = false, length = 5 });
            this.candidateFields.Add(new CandidateField() { name = "houseNumber", type = "esrifieldTypeString", alias = "houseNumber", required = false, length = 12 });
            this.candidateFields.Add(new CandidateField() { name = "firstStreetNameNormalized", type = "esrifieldTypeString", alias = "firstStreetNameNormalized", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "secondStreetNameNormalized", type = "esrifieldTypeString", alias = "secondStreetNameNormalized", required = false, length = 24 });
            this.candidateFields.Add(new CandidateField() { name = "buildingIdentificationNumber", type = "esrifieldTypeString", alias = "buildingIdentificationNumber", required = false, length = 20 });
            this.candidateFields.Add(new CandidateField() { name = "firstBoroughName", type = "esrifieldTypeString", alias = "firstBoroughName", required = false, length = 16 });

            this.locatorProperties = new LocatorProperties()
            {
                IntersectionConnectors = "&amp; @ | and",
                SuggestedBatchSize = 150,
                MaxBatchSize = 500,
                LoadBalancerTimeOut = 60,
                WriteXYCoordFields = "TRUE",
                WriteStandardizedAddressField = "FALSE",
                WriteReferenceIDField = "FALSE",
                WritePercentAlongField = "FALSE",
                UICLSID = "{4A586727-7BCF-4A1X-83DB-F02D437HB8EC}"
            };

            this.countries.Add("US");
            this.capabilities = "Geocode";
        }
    }
}