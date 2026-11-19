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

export interface VehicleVulnerability {
  motorcycleHighRisk: boolean;
  sedanHighRisk: boolean;
  suvHighRisk: boolean;
  parkingAdvisory: string;
}

export interface TenantDueDiligence {
  executiveSummary: string;
  groundFloorRiskAdvice: string;
  electricalAndUtilityRisk: string;
  evacuationAndAccessAdvisory: string;
}

export interface PropertyAuditResponse {
  address: string;
  resolvedLocationName: string;
  latitude: number;
  longitude: number;
  riskScore: number;
  riskLevel: 'Low' | 'Moderate' | 'High' | 'Extreme';
  historicalIncidentsCount: number;
  averageDepthInches: number;
  maxDepthInches: number;
  predominantDepthCategory: string;
  estimatedRecedingTimeHours: number;
  vehicleVulnerability: VehicleVulnerability;
  dueDiligenceReport: TenantDueDiligence;
  auditedAt: string;
}

export interface ConfirmReportRequest {
  stillFlooded: boolean;
  reporterAlias?: string;
}

export interface ConfirmReportResponse {
  reportId: string;
  status: string;
  expiresAt: string;
  message: string;
}

export interface NewsIngestionResult {
  totalItemsPolled: number;
  floodIncidentsDetected: number;
  newReportsCreated: number;
  duplicatesMerged: number;
  itemsSkippedNonFlood: number;
  createdReports: FloodReport[];
  mergedReports: FloodReport[];
  processedAt: string;
}

