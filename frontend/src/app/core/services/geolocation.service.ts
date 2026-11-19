import { Injectable, signal } from '@angular/core';

export interface GeoLocationCoords {
  lat: number;
  lng: number;
  accuracy?: number;
}

export interface LandmarkPreset {
  name: string;
  district: string;
  lat: number;
  lng: number;
}

@Injectable({
  providedIn: 'root'
})
export class GeolocationService {
  // Default centered at Manila (España Blvd / UST)
  public readonly defaultCoords: GeoLocationCoords = { lat: 14.6091, lng: 120.9903 };
  public readonly currentCoords = signal<GeoLocationCoords>(this.defaultCoords);
  public readonly isGpsActive = signal<boolean>(false);

  public readonly presets: LandmarkPreset[] = [
    { name: 'España Blvd (UST)', district: 'Sampaloc, Manila', lat: 14.6091, lng: 120.9903 },
    { name: 'Taft Ave (Pedro Gil)', district: 'Malate, Manila', lat: 14.5775, lng: 120.9880 },
    { name: 'Quiapo (Quezon Blvd)', district: 'Quiapo, Manila', lat: 14.5995, lng: 120.9842 },
    { name: 'EDSA (Shaw Blvd)', district: 'Mandaluyong', lat: 14.5869, lng: 121.0543 },
    { name: 'G. Araneta Ave', district: 'Quezon City', lat: 14.6280, lng: 121.0110 }
  ];

  constructor() {
    this.requestCurrentPosition();
  }

  public requestCurrentPosition(): void {
    if (!('geolocation' in navigator)) {
      console.warn('Geolocation not supported by browser. Using Manila default.');
      return;
    }

    navigator.geolocation.getCurrentPosition(
      (position) => {
        const coords = {
          lat: position.coords.latitude,
          lng: position.coords.longitude,
          accuracy: position.coords.accuracy
        };
        this.currentCoords.set(coords);
        this.isGpsActive.set(true);
        console.log('Browser GPS acquired:', coords);
      },
      (error) => {
        console.warn('Geolocation access denied or timed out:', error.message);
        this.isGpsActive.set(false);
      },
      {
        enableHighAccuracy: true,
        timeout: 8000,
        maximumAge: 60000
      }
    );
  }

  public selectPreset(preset: LandmarkPreset): void {
    this.currentCoords.set({ lat: preset.lat, lng: preset.lng });
  }
}
