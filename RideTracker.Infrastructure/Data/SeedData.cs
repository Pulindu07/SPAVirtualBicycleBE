using RideTracker.Domain.Entities;

namespace RideTracker.Infrastructure.Data;

public static class SeedData
{
    public static Route GetDefaultRoute()
    {
        return new Route
        {
            Name = "Dondra Head to Point Pedro",
            Description = "Journey 572 km from Dondra Head to Point Pedro",
            TotalDistanceKm = 572.0,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public static List<RoutePoint> GetSriLankaCoastalRoute(int routeId)
    {
        // Coastal route around Sri Lanka: Matara → Galle → Colombo → Jaffna → Trincomalee → Matara
        // This is a simplified route with key points along the coast
        return new List<RoutePoint>
        {
            // Starting point: Matara (South)
            new RoutePoint { RouteId = routeId, OrderIndex = 0, Latitude = 5.9549, Longitude = 80.5550 },
            
            // Matara to Galle (West along south coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 1, Latitude = 5.9485, Longitude = 80.4854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 2, Latitude = 5.9615, Longitude = 80.3250 },
            new RoutePoint { RouteId = routeId, OrderIndex = 3, Latitude = 6.0328, Longitude = 80.2170 },
            new RoutePoint { RouteId = routeId, OrderIndex = 4, Latitude = 6.0535, Longitude = 80.2210 }, // Galle
            
            // Galle to Colombo (North along west coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 5, Latitude = 6.1354, Longitude = 80.0992 },
            new RoutePoint { RouteId = routeId, OrderIndex = 6, Latitude = 6.2384, Longitude = 80.0053 },
            new RoutePoint { RouteId = routeId, OrderIndex = 7, Latitude = 6.4218, Longitude = 79.8652 },
            new RoutePoint { RouteId = routeId, OrderIndex = 8, Latitude = 6.5854, Longitude = 79.8607 },
            new RoutePoint { RouteId = routeId, OrderIndex = 9, Latitude = 6.7273, Longitude = 79.8612 },
            new RoutePoint { RouteId = routeId, OrderIndex = 10, Latitude = 6.9271, Longitude = 79.8612 }, // Colombo
            
            // Colombo to Puttalam (North along west coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 11, Latitude = 7.1025, Longitude = 79.8564 },
            new RoutePoint { RouteId = routeId, OrderIndex = 12, Latitude = 7.2906, Longitude = 79.8538 },
            new RoutePoint { RouteId = routeId, OrderIndex = 13, Latitude = 7.4818, Longitude = 79.8285 },
            new RoutePoint { RouteId = routeId, OrderIndex = 14, Latitude = 7.6521, Longitude = 79.8394 },
            new RoutePoint { RouteId = routeId, OrderIndex = 15, Latitude = 7.9569, Longitude = 79.8285 },
            new RoutePoint { RouteId = routeId, OrderIndex = 16, Latitude = 8.0362, Longitude = 79.8285 }, // Puttalam
            
            // Puttalam to Mannar (Northwest)
            new RoutePoint { RouteId = routeId, OrderIndex = 17, Latitude = 8.2528, Longitude = 79.9047 },
            new RoutePoint { RouteId = routeId, OrderIndex = 18, Latitude = 8.4650, Longitude = 79.9856 },
            new RoutePoint { RouteId = routeId, OrderIndex = 19, Latitude = 8.6521, Longitude = 80.0125 },
            new RoutePoint { RouteId = routeId, OrderIndex = 20, Latitude = 8.8542, Longitude = 79.9542 },
            new RoutePoint { RouteId = routeId, OrderIndex = 21, Latitude = 8.9812, Longitude = 79.9047 }, // Mannar
            
            // Mannar to Jaffna (North)
            new RoutePoint { RouteId = routeId, OrderIndex = 22, Latitude = 9.1258, Longitude = 79.9285 },
            new RoutePoint { RouteId = routeId, OrderIndex = 23, Latitude = 9.3854, Longitude = 80.0542 },
            new RoutePoint { RouteId = routeId, OrderIndex = 24, Latitude = 9.6612, Longitude = 80.0256 }, // Jaffna
            
            // Jaffna to Mullaitivu (East along north coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 25, Latitude = 9.6521, Longitude = 80.1854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 26, Latitude = 9.5854, Longitude = 80.3625 },
            new RoutePoint { RouteId = routeId, OrderIndex = 27, Latitude = 9.5125, Longitude = 80.5285 },
            new RoutePoint { RouteId = routeId, OrderIndex = 28, Latitude = 9.2685, Longitude = 80.8142 }, // Mullaitivu
            
            // Mullaitivu to Trincomalee (South along east coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 29, Latitude = 9.0854, Longitude = 80.9625 },
            new RoutePoint { RouteId = routeId, OrderIndex = 30, Latitude = 8.8521, Longitude = 81.0854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 31, Latitude = 8.5869, Longitude = 81.2142 }, // Trincomalee
            
            // Trincomalee to Batticaloa (South)
            new RoutePoint { RouteId = routeId, OrderIndex = 32, Latitude = 8.4125, Longitude = 81.1854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 33, Latitude = 8.2542, Longitude = 81.1625 },
            new RoutePoint { RouteId = routeId, OrderIndex = 34, Latitude = 8.0521, Longitude = 81.1854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 35, Latitude = 7.7208, Longitude = 81.6854 }, // Batticaloa
            
            // Batticaloa to Pottuvil (South along east coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 36, Latitude = 7.4854, Longitude = 81.7285 },
            new RoutePoint { RouteId = routeId, OrderIndex = 37, Latitude = 7.2125, Longitude = 81.7854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 38, Latitude = 6.9285, Longitude = 81.8285 },
            new RoutePoint { RouteId = routeId, OrderIndex = 39, Latitude = 6.8701, Longitude = 81.8354 }, // Arugam Bay
            new RoutePoint { RouteId = routeId, OrderIndex = 40, Latitude = 6.6854, Longitude = 81.7625 },
            
            // Pottuvil to Hambantota (Southwest along south coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 41, Latitude = 6.4285, Longitude = 81.5854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 42, Latitude = 6.2854, Longitude = 81.3625 },
            new RoutePoint { RouteId = routeId, OrderIndex = 43, Latitude = 6.1854, Longitude = 81.1854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 44, Latitude = 6.1245, Longitude = 81.1211 }, // Hambantota
            
            // Hambantota to Matara (West along south coast)
            new RoutePoint { RouteId = routeId, OrderIndex = 45, Latitude = 6.0854, Longitude = 80.9625 },
            new RoutePoint { RouteId = routeId, OrderIndex = 46, Latitude = 6.0125, Longitude = 80.7854 },
            new RoutePoint { RouteId = routeId, OrderIndex = 47, Latitude = 5.9549, Longitude = 80.5550 }  // Back to Matara (completing the loop)
        };
    }
}

