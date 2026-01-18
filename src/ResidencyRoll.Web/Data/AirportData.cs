namespace ResidencyRoll.Web.Data;

/// <summary>
/// Airport data with IATA codes, cities, countries, and IANA timezone identifiers.
/// Data sourced from OpenFlights and validated against IANA timezone database.
/// </summary>
public class AirportData
{
    public string IataCode { get; set; } = string.Empty;
    public string AirportName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string IanaTimezone { get; set; } = string.Empty;
    
    public string DisplayName => $"{IataCode} - {AirportName}, {City}";
    public string SearchText => $"{IataCode} {City} {AirportName} {Country}".ToLower();

    public AirportData(string iataCode, string airportName, string city, string country, string ianaTimezone)
    {
        IataCode = iataCode;
        AirportName = airportName;
        City = city;
        Country = country;
        IanaTimezone = ianaTimezone;
    }
}

public static class AirportDatabase
{
    private static readonly List<AirportData> _airports = new()
    {
        // CANADA - Major Airports
        new("YYZ", "Toronto Pearson International", "Toronto", "Canada", "America/Toronto"),
        new("YUL", "Montreal-Pierre Elliott Trudeau International", "Montreal", "Canada", "America/Toronto"),
        new("YVR", "Vancouver International", "Vancouver", "Canada", "America/Vancouver"),
        new("YYC", "Calgary International", "Calgary", "Canada", "America/Edmonton"),
        new("YOW", "Ottawa Macdonald-Cartier International", "Ottawa", "Canada", "America/Toronto"),
        new("YEG", "Edmonton International", "Edmonton", "Canada", "America/Edmonton"),
        new("YWG", "Winnipeg James Armstrong Richardson International", "Winnipeg", "Canada", "America/Winnipeg"),
        new("YHZ", "Halifax Stanfield International", "Halifax", "Canada", "America/Halifax"),
        new("YYJ", "Victoria International", "Victoria", "Canada", "America/Vancouver"),
        new("YQB", "Quebec City Jean Lesage International", "Quebec City", "Canada", "America/Toronto"),
        new("YXE", "Saskatoon John G. Diefenbaker International", "Saskatoon", "Canada", "America/Regina"),
        new("YYT", "St. John's International", "St. John's", "Canada", "America/St_Johns"),
        new("YQR", "Regina International", "Regina", "Canada", "America/Regina"),
        new("YHM", "John C. Munro Hamilton International", "Hamilton", "Canada", "America/Toronto"),
        new("YKF", "Waterloo Regional", "Kitchener", "Canada", "America/Toronto"),
        
        // UNITED STATES - Major Airports
        new("JFK", "John F. Kennedy International", "New York", "United States", "America/New_York"),
        new("LGA", "LaGuardia", "New York", "United States", "America/New_York"),
        new("EWR", "Newark Liberty International", "Newark", "United States", "America/New_York"),
        new("LAX", "Los Angeles International", "Los Angeles", "United States", "America/Los_Angeles"),
        new("ORD", "O'Hare International", "Chicago", "United States", "America/Chicago"),
        new("DFW", "Dallas/Fort Worth International", "Dallas", "United States", "America/Chicago"),
        new("DEN", "Denver International", "Denver", "United States", "America/Denver"),
        new("SFO", "San Francisco International", "San Francisco", "United States", "America/Los_Angeles"),
        new("SEA", "Seattle-Tacoma International", "Seattle", "United States", "America/Los_Angeles"),
        new("LAS", "Harry Reid International", "Las Vegas", "United States", "America/Los_Angeles"),
        new("MCO", "Orlando International", "Orlando", "United States", "America/New_York"),
        new("MIA", "Miami International", "Miami", "United States", "America/New_York"),
        new("ATL", "Hartsfield-Jackson Atlanta International", "Atlanta", "United States", "America/New_York"),
        new("BOS", "Logan International", "Boston", "United States", "America/New_York"),
        new("PHX", "Phoenix Sky Harbor International", "Phoenix", "United States", "America/Phoenix"),
        new("IAH", "George Bush Intercontinental", "Houston", "United States", "America/Chicago"),
        new("MSP", "Minneapolis-St. Paul International", "Minneapolis", "United States", "America/Chicago"),
        new("DTW", "Detroit Metropolitan Wayne County", "Detroit", "United States", "America/Detroit"),
        new("PHL", "Philadelphia International", "Philadelphia", "United States", "America/New_York"),
        new("PDX", "Portland International", "Portland", "United States", "America/Los_Angeles"),
        new("SAN", "San Diego International", "San Diego", "United States", "America/Los_Angeles"),
        new("HNL", "Daniel K. Inouye International", "Honolulu", "United States", "Pacific/Honolulu"),
        new("ANC", "Ted Stevens Anchorage International", "Anchorage", "United States", "America/Anchorage"),
        
        // MEXICO
        new("MEX", "Mexico City International", "Mexico City", "Mexico", "America/Mexico_City"),
        new("CUN", "Cancun International", "Cancun", "Mexico", "America/Cancun"),
        new("GDL", "Guadalajara International", "Guadalajara", "Mexico", "America/Mexico_City"),
        new("MTY", "Monterrey International", "Monterrey", "Mexico", "America/Monterrey"),
        new("TIJ", "Tijuana International", "Tijuana", "Mexico", "America/Tijuana"),
        
        // UNITED KINGDOM
        new("LHR", "London Heathrow", "London", "United Kingdom", "Europe/London"),
        new("LGW", "London Gatwick", "London", "United Kingdom", "Europe/London"),
        new("MAN", "Manchester", "Manchester", "United Kingdom", "Europe/London"),
        new("EDI", "Edinburgh", "Edinburgh", "United Kingdom", "Europe/London"),
        new("BHX", "Birmingham", "Birmingham", "United Kingdom", "Europe/London"),
        new("GLA", "Glasgow", "Glasgow", "United Kingdom", "Europe/London"),
        
        // EUROPE - Major Hubs
        new("CDG", "Charles de Gaulle", "Paris", "France", "Europe/Paris"),
        new("FRA", "Frankfurt", "Frankfurt", "Germany", "Europe/Berlin"),
        new("AMS", "Amsterdam Schiphol", "Amsterdam", "Netherlands", "Europe/Amsterdam"),
        new("MAD", "Adolfo Suárez Madrid-Barajas", "Madrid", "Spain", "Europe/Madrid"),
        new("BCN", "Barcelona-El Prat", "Barcelona", "Spain", "Europe/Madrid"),
        new("FCO", "Leonardo da Vinci-Fiumicino", "Rome", "Italy", "Europe/Rome"),
        new("MXP", "Milan Malpensa", "Milan", "Italy", "Europe/Rome"),
        new("MUC", "Munich", "Munich", "Germany", "Europe/Berlin"),
        new("ZRH", "Zurich", "Zurich", "Switzerland", "Europe/Zurich"),
        new("VIE", "Vienna International", "Vienna", "Austria", "Europe/Vienna"),
        new("CPH", "Copenhagen", "Copenhagen", "Denmark", "Europe/Copenhagen"),
        new("ARN", "Stockholm Arlanda", "Stockholm", "Sweden", "Europe/Stockholm"),
        new("OSL", "Oslo Gardermoen", "Oslo", "Norway", "Europe/Oslo"),
        new("HEL", "Helsinki-Vantaa", "Helsinki", "Finland", "Europe/Helsinki"),
        new("BRU", "Brussels", "Brussels", "Belgium", "Europe/Brussels"),
        new("DUB", "Dublin", "Dublin", "Ireland", "Europe/Dublin"),
        new("LIS", "Lisbon Portela", "Lisbon", "Portugal", "Europe/Lisbon"),
        new("PRG", "Václav Havel Prague", "Prague", "Czech Republic", "Europe/Prague"),
        new("WAW", "Warsaw Chopin", "Warsaw", "Poland", "Europe/Warsaw"),
        new("BUD", "Budapest Ferenc Liszt International", "Budapest", "Hungary", "Europe/Budapest"),
        new("ATH", "Athens International", "Athens", "Greece", "Europe/Athens"),
        new("IST", "Istanbul", "Istanbul", "Turkey", "Europe/Istanbul"),
        
        // ASIA - Major Hubs
        new("NRT", "Narita International", "Tokyo", "Japan", "Asia/Tokyo"),
        new("HND", "Tokyo Haneda", "Tokyo", "Japan", "Asia/Tokyo"),
        new("KIX", "Kansai International", "Osaka", "Japan", "Asia/Tokyo"),
        new("ICN", "Incheon International", "Seoul", "South Korea", "Asia/Seoul"),
        new("PEK", "Beijing Capital International", "Beijing", "China", "Asia/Shanghai"),
        new("PVG", "Shanghai Pudong International", "Shanghai", "China", "Asia/Shanghai"),
        new("HKG", "Hong Kong International", "Hong Kong", "Hong Kong", "Asia/Hong_Kong"),
        new("SIN", "Singapore Changi", "Singapore", "Singapore", "Asia/Singapore"),
        new("BKK", "Suvarnabhumi", "Bangkok", "Thailand", "Asia/Bangkok"),
        new("KUL", "Kuala Lumpur International", "Kuala Lumpur", "Malaysia", "Asia/Kuala_Lumpur"),
        new("MNL", "Ninoy Aquino International", "Manila", "Philippines", "Asia/Manila"),
        new("CGK", "Soekarno-Hatta International", "Jakarta", "Indonesia", "Asia/Jakarta"),
        new("DEL", "Indira Gandhi International", "Delhi", "India", "Asia/Kolkata"),
        new("BOM", "Chhatrapati Shivaji Maharaj International", "Mumbai", "India", "Asia/Kolkata"),
        new("BLR", "Kempegowda International", "Bangalore", "India", "Asia/Kolkata"),
        new("DXB", "Dubai International", "Dubai", "United Arab Emirates", "Asia/Dubai"),
        new("TLV", "Ben Gurion", "Tel Aviv", "Israel", "Asia/Tel_Aviv"),
        
        // AUSTRALIA & NEW ZEALAND
        new("SYD", "Sydney Kingsford Smith", "Sydney", "Australia", "Australia/Sydney"),
        new("MEL", "Melbourne", "Melbourne", "Australia", "Australia/Melbourne"),
        new("BNE", "Brisbane", "Brisbane", "Australia", "Australia/Brisbane"),
        new("PER", "Perth", "Perth", "Australia", "Australia/Perth"),
        new("ADL", "Adelaide", "Adelaide", "Australia", "Australia/Adelaide"),
        new("AKL", "Auckland", "Auckland", "New Zealand", "Pacific/Auckland"),
        new("WLG", "Wellington", "Wellington", "New Zealand", "Pacific/Auckland"),
        new("CHC", "Christchurch", "Christchurch", "New Zealand", "Pacific/Auckland"),
        
        // SOUTH AMERICA
        new("GRU", "São Paulo-Guarulhos International", "São Paulo", "Brazil", "America/Sao_Paulo"),
        new("GIG", "Rio de Janeiro-Galeão International", "Rio de Janeiro", "Brazil", "America/Sao_Paulo"),
        new("EZE", "Ministro Pistarini International", "Buenos Aires", "Argentina", "America/Argentina/Buenos_Aires"),
        new("SCL", "Arturo Merino Benítez International", "Santiago", "Chile", "America/Santiago"),
        new("LIM", "Jorge Chávez International", "Lima", "Peru", "America/Lima"),
        new("BOG", "El Dorado International", "Bogotá", "Colombia", "America/Bogota"),
        
        // AFRICA
        new("CAI", "Cairo International", "Cairo", "Egypt", "Africa/Cairo"),
        new("JNB", "O.R. Tambo International", "Johannesburg", "South Africa", "Africa/Johannesburg"),
        new("CPT", "Cape Town International", "Cape Town", "South Africa", "Africa/Johannesburg"),
        new("LOS", "Murtala Muhammed International", "Lagos", "Nigeria", "Africa/Lagos"),
        new("NBO", "Jomo Kenyatta International", "Nairobi", "Kenya", "Africa/Nairobi"),
        new("ADD", "Addis Ababa Bole International", "Addis Ababa", "Ethiopia", "Africa/Addis_Ababa"),
        new("CAS", "Mohammed V International", "Casablanca", "Morocco", "Africa/Casablanca"),
    };

    public static List<AirportData> GetAllAirports() => _airports;

    public static AirportData? FindByIataCode(string iataCode)
    {
        return _airports.FirstOrDefault(a => 
            a.IataCode.Equals(iataCode, StringComparison.OrdinalIgnoreCase));
    }

    public static List<AirportData> SearchAirports(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return _airports;

        var term = searchTerm.ToLower();
        return _airports
            .Where(a => a.SearchText.Contains(term))
            .OrderBy(a => 
            {
                // Prioritize IATA code exact matches
                if (a.IataCode.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                    return 0;
                // Then city name starts with
                if (a.City.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                    return 1;
                // Then other matches
                return 2;
            })
            .ThenBy(a => a.City)
            .ToList();
    }
}
