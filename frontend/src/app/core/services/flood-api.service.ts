import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConfirmReportRequest,
  ConfirmReportResponse,
  CreateReportRequest,
  FloodReport,
  NearbyReportsQuery,
  NewsIngestionResult,
  PropertyAuditResponse
} from '../models/flood-report.model';

@Injectable({
  providedIn: 'root'
})
export class FloodApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  getNearbyReports(query: NearbyReportsQuery): Observable<FloodReport[]> {
    let params = new HttpParams()
      .set('lat', query.lat.toString())
      .set('lng', query.lng.toString())
      .set('radiusMeters', (query.radiusMeters ?? 2500).toString());

    if (query.vehicleType && query.vehicleType !== 'All') {
      params = params.set('vehicleType', query.vehicleType.toLowerCase());
    }

    return this.http.get<FloodReport[]>(`${this.baseUrl}/reports/nearby`, { params });
  }

  getReportById(id: string): Observable<FloodReport> {
    return this.http.get<FloodReport>(`${this.baseUrl}/reports/${id}`);
  }

  submitReport(request: CreateReportRequest): Observable<FloodReport> {
    const formData = new FormData();
    formData.append('RawDescription', request.rawDescription);
    formData.append('Latitude', request.latitude.toString());
    formData.append('Longitude', request.longitude.toString());

    if (request.reporterAlias) {
      formData.append('ReporterAlias', request.reporterAlias);
    }
    if (request.barangay) {
      formData.append('Barangay', request.barangay);
    }
    if (request.city) {
      formData.append('City', request.city);
    }
    if (request.photo) {
      formData.append('Photo', request.photo, request.photo.name);
    }

    return this.http.post<FloodReport>(`${this.baseUrl}/reports`, formData);
  }

  confirmReport(id: string, request: ConfirmReportRequest): Observable<ConfirmReportResponse> {
    return this.http.post<ConfirmReportResponse>(`${this.baseUrl}/reports/${id}/confirm`, request);
  }

  getPropertyAudit(address: string, lat?: number, lng?: number, radiusMeters: number = 500): Observable<PropertyAuditResponse> {
    let params = new HttpParams()
      .set('address', address)
      .set('radiusMeters', radiusMeters.toString());

    if (lat !== undefined && lng !== undefined) {
      params = params.set('lat', lat.toString()).set('lng', lng.toString());
    }
    return this.http.get<PropertyAuditResponse>(`${this.baseUrl}/property-audit`, { params });
  }

  ingestNewsAlerts(): Observable<NewsIngestionResult> {
    return this.http.post<NewsIngestionResult>(`${this.baseUrl}/newsalerts/ingest`, {});
  }

  getRecentOfficialAlerts(limit: number = 20): Observable<FloodReport[]> {
    return this.http.get<FloodReport[]>(`${this.baseUrl}/newsalerts/recent?limit=${limit}`);
  }
}
