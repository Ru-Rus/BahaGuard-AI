export type VehicleType = 'All' | 'Motorcycle' | 'Sedan' | 'Suv';

export type DepthCategory = 'Dry' | 'Gutter' | 'HalfTire' | 'Knee' | 'Waist' | 'ChestPlus';

export interface Passability {
  motorcycle: boolean;
  sedan: boolean;
  suvOrJeepney: boolean;
}

export interface FloodReport {
  id: string;
  reporterAlias?: string;
  rawDescription: string;
  extractedLocationName: string;
  barangay?: string;
  city: string;
  latitude: number;
  longitude: number;
  estimatedDepthInches: number;
  depthCategory: DepthCategory;
  passableMotorcycle: boolean;
  passableSedan: boolean;
  passableSuvJeepney: boolean;
  aiConfidenceScore: number;
  imageUrl?: string;
  status: string;
  createdAt: string;
  expiresAt: string;
  distanceMeters?: number;
}

export interface NearbyReportsQuery {
  lat: number;
  lng: number;
  radiusMeters?: number;
  vehicleType?: string;
}

export interface CreateReportRequest {
  rawDescription: string;
  latitude: number;
  longitude: number;
  reporterAlias?: string;
  barangay?: string;
  city?: string;
  photo?: File;
}
