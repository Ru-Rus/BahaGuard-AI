import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CreateReportRequest, FloodReport, NearbyReportsQuery } from '../models/flood-report.model';

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

  getPropertyAudit(address: string, lat?: number, lng?: number): Observable<any> {
    let params = new HttpParams().set('address', address);
    if (lat !== undefined && lng !== undefined) {
      params = params.set('lat', lat.toString()).set('lng', lng.toString());
    }
    return this.http.get<any>(`${this.baseUrl}/property-audit`, { params });
  }
}
