import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  OnInit,
  signal,
  viewChild
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import * as L from 'leaflet';
import { FloodReport, VehicleType } from '../../core/models/flood-report.model';
import { FloodApiService } from '../../core/services/flood-api.service';
import { GeolocationService, LandmarkPreset } from '../../core/services/geolocation.service';
import { SignalRService } from '../../core/services/signalr.service';
import { DepthIndicatorComponent } from '../../shared/components/depth-indicator.component';
import { PassabilityBadgeComponent } from '../../shared/components/passability-badge.component';
import { ReportIncidentComponent } from '../report-incident/report-incident.component';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-live-map',
  standalone: true,
  imports: [
    DepthIndicatorComponent,
    PassabilityBadgeComponent,
    ReportIncidentComponent,
    RouterLink
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative w-full h-[100dvh] overflow-hidden bg-slate-950 font-sans text-slate-100 flex flex-col">
      <!-- Top Navigation & Live Header -->
      <header class="absolute top-0 left-0 right-0 z-30 flex items-center justify-between px-3 sm:px-4 py-2.5 sm:py-3 bg-slate-900/90 backdrop-blur-md border-b border-slate-800 shadow-xl">
        <div class="flex items-center gap-2.5 sm:gap-3">
          <div class="w-9 h-9 sm:w-10 sm:h-10 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-500 flex items-center justify-center shadow-lg shadow-cyan-500/20 flex-shrink-0">
            <span class="text-xl sm:text-2xl">🌊</span>
          </div>
          <div>
            <div class="flex items-center gap-1.5 sm:gap-2">
              <h1 class="font-extrabold text-base sm:text-lg text-white tracking-tight">BahaGuard AI</h1>
              <span class="px-1.5 py-0.5 text-[9px] sm:text-[10px] font-bold uppercase rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
                <span class="sm:hidden">MM</span>
                <span class="hidden sm:inline">Metro Manila</span>
              </span>
            </div>
            <p class="hidden sm:block text-xs text-slate-400">Live Crowdsourced & AI-Verified Flood Passability</p>
          </div>
        </div>

        <!-- Real-Time SignalR Status Indicator -->
        <div class="hidden md:flex items-center gap-4 text-xs font-medium">
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-full bg-slate-800/80 border border-slate-700">
            <span
              class="w-2.5 h-2.5 rounded-full"
              [class]="signalrService.isConnected() ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400'"
            ></span>
            <span class="text-slate-300">
              {{ signalrService.isConnected() ? 'Live SignalR Connected' : 'Connecting to Alerts...' }}
            </span>
          </div>
        </div>

        <!-- Action Buttons: Audit & Report Flood -->
        <div class="flex items-center gap-2">
          <!-- Mobile SignalR Dot -->
          <div class="md:hidden flex items-center pr-1" title="SignalR live alert stream">
            <span
              class="w-2.5 h-2.5 rounded-full"
              [class]="signalrService.isConnected() ? 'bg-emerald-400 animate-pulse' : 'bg-amber-400'"
            ></span>
          </div>

          <!-- Property Auditor Link -->
          <a
            routerLink="/audit"
            class="flex items-center gap-1 px-2.5 sm:px-3 py-1.5 sm:py-2 rounded-xl bg-slate-800/80 hover:bg-slate-700 text-slate-300 hover:text-white font-medium text-xs border border-slate-700/80 transition shadow-sm active:scale-95"
          >
            <span>🛡️</span>
            <span class="hidden xs:inline sm:inline">Auditor</span>
          </a>

          <!-- Desktop Report Flood Button -->
          <button
            (click)="isReportModalOpen.set(true)"
            class="hidden sm:flex items-center gap-2 px-3.5 py-2 rounded-xl bg-gradient-to-r from-cyan-500 to-blue-600 hover:from-cyan-400 hover:to-blue-500 text-white font-bold text-xs sm:text-sm shadow-lg shadow-cyan-500/25 transition active:scale-95"
          >
            <span>📸</span>
            <span>Report Flood</span>
          </button>
        </div>
      </header>

      <!-- Floating Controls: Vehicle Selector & Horizontal Hotspots -->
      <div class="absolute top-14 sm:top-20 left-1/2 -translate-x-1/2 z-20 w-[96%] max-w-xl flex flex-col gap-1.5 items-center pointer-events-none">
        <!-- Vehicle Filter Bar -->
        <div class="p-1 sm:p-1.5 rounded-2xl bg-slate-900/90 backdrop-blur-md border border-slate-700/80 shadow-2xl flex items-center justify-between w-full sm:w-auto sm:justify-center gap-1 pointer-events-auto">
          <div class="px-2.5 py-1 text-xs font-semibold text-slate-400 uppercase tracking-wider hidden lg:block">
            Vehicle:
          </div>

          <button
            (click)="setVehicleFilter('All')"
            [class]="selectedVehicle() === 'All'
              ? 'bg-cyan-500 text-slate-950 shadow-md font-bold'
              : 'text-slate-300 hover:bg-slate-800'"
            class="flex-1 sm:flex-none flex items-center justify-center gap-1 px-2.5 sm:px-3 py-1.5 rounded-xl text-xs transition"
          >
            <span>🧭</span>
            <span class="hidden sm:inline">All Floods</span>
            <span class="sm:hidden">All</span>
          </button>

          <button
            (click)="setVehicleFilter('Motorcycle')"
            [class]="selectedVehicle() === 'Motorcycle'
              ? 'bg-amber-500 text-slate-950 shadow-md font-bold'
              : 'text-slate-300 hover:bg-slate-800'"
            class="flex-1 sm:flex-none flex items-center justify-center gap-1 px-2.5 sm:px-3 py-1.5 rounded-xl text-xs transition"
          >
            <span>🏍️</span>
            <span class="hidden sm:inline">Motorcycle</span>
            <span class="sm:hidden">Moto</span>
          </button>

          <button
            (click)="setVehicleFilter('Sedan')"
            [class]="selectedVehicle() === 'Sedan'
              ? 'bg-blue-500 text-slate-950 shadow-md font-bold'
              : 'text-slate-300 hover:bg-slate-800'"
            class="flex-1 sm:flex-none flex items-center justify-center gap-1 px-2.5 sm:px-3 py-1.5 rounded-xl text-xs transition"
          >
            <span>🚗</span>
            <span>Sedan</span>
          </button>

          <button
            (click)="setVehicleFilter('Suv')"
            [class]="selectedVehicle() === 'Suv'
              ? 'bg-emerald-500 text-slate-950 shadow-md font-bold'
              : 'text-slate-300 hover:bg-slate-800'"
            class="flex-1 sm:flex-none flex items-center justify-center gap-1 px-2.5 sm:px-3 py-1.5 rounded-xl text-xs transition"
          >
            <span>🚙</span>
            <span class="hidden sm:inline">SUV / Jeep</span>
            <span class="sm:hidden">SUV</span>
          </button>
        </div>

        <!-- Horizontal Hotspots Bar (Touch Friendly on Mobile) -->
        <div class="w-full flex items-center justify-start sm:justify-center gap-1.5 overflow-x-auto no-scrollbar py-1 px-1 pointer-events-auto">
          @for (preset of geoService.presets; track preset.name) {
            <button
              (click)="jumpToPreset(preset)"
              class="whitespace-nowrap flex-shrink-0 px-2.5 py-1 rounded-xl text-[11px] font-medium bg-slate-900/85 hover:bg-cyan-600 hover:text-white backdrop-blur-md border border-slate-700/80 text-slate-300 shadow-md transition active:scale-95"
            >
              {{ preset.name.split(' ')[0] }}
            </button>
          }
        </div>
      </div>

      <!-- Live Map Container -->
      <div #mapContainer class="w-full flex-1 touch-pan-x touch-pan-y"></div>

      <!-- Floating Bottom Action Cluster (Thumb-Zone Navigation for Mobile) -->
      <div class="absolute bottom-5 right-4 z-20 flex flex-col items-end gap-2.5 pointer-events-auto">
        <!-- GPS Re-Center Button -->
        <button
          (click)="recenterToGps()"
          title="Re-center to my location"
          aria-label="Re-center to my location"
          class="w-11 h-11 rounded-2xl bg-slate-900/90 hover:bg-slate-800 text-cyan-400 border border-slate-700 shadow-2xl flex items-center justify-center transition active:scale-95 backdrop-blur-sm"
        >
          <span class="text-xl">📍</span>
        </button>

        <!-- Mobile Legend Toggle Button -->
        <button
          (click)="showMobileLegend.set(!showMobileLegend())"
          title="Toggle Passability Legend"
          aria-label="Toggle Passability Legend"
          class="lg:hidden w-11 h-11 rounded-2xl bg-slate-900/90 hover:bg-slate-800 text-amber-400 border border-slate-700 shadow-2xl flex items-center justify-center transition active:scale-95 backdrop-blur-sm"
        >
          <span class="text-lg">🚦</span>
        </button>

        <!-- Mobile Floating Action Report Button -->
        <button
          (click)="isReportModalOpen.set(true)"
          class="sm:hidden flex items-center gap-2 px-4 py-3 rounded-2xl bg-gradient-to-r from-cyan-500 to-blue-600 hover:from-cyan-400 hover:to-blue-500 text-white font-bold text-xs shadow-2xl shadow-cyan-500/40 active:scale-95 transition"
        >
          <span class="text-base">📸</span>
          <span>Report Flood</span>
        </button>
      </div>

      <!-- Mobile Passability Legend Drawer -->
      @if (showMobileLegend()) {
        <div class="lg:hidden absolute bottom-20 left-4 right-4 z-30 p-4 rounded-2xl bg-slate-900/95 backdrop-blur-lg border border-slate-700 shadow-2xl text-xs animate-in slide-in-from-bottom duration-200">
          <div class="flex items-center justify-between pb-2 border-b border-slate-800">
            <h4 class="font-bold text-white flex items-center gap-1.5">
              <span>🚦</span>
              <span>Passability Legend ({{ selectedVehicle() }})</span>
            </h4>
            <button (click)="showMobileLegend.set(false)" class="text-slate-400 hover:text-white p-1">✕</button>
          </div>
          <div class="mt-2.5 space-y-2">
            <div class="flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-emerald-500"></span>
              <span class="text-slate-200">Safe / Passable for {{ selectedVehicle() }}</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-amber-500"></span>
              <span class="text-slate-200">Caution (Tire-deep water level)</span>
            </div>
            <div class="flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-rose-500"></span>
              <span class="text-slate-200">Hazard / Submerged (Impassable)</span>
            </div>
          </div>
        </div>
      }

      <!-- Real-Time Incoming Incident Toast Alert -->
      @if (recentBroadcast(); as liveAlert) {
        <div class="absolute bottom-20 sm:bottom-6 left-3 right-3 sm:left-auto sm:right-6 z-30 sm:max-w-sm p-4 rounded-2xl bg-slate-900/95 backdrop-blur-lg border border-cyan-500/50 shadow-2xl animate-in slide-in-from-bottom-3 duration-200">
          <div class="flex items-start justify-between gap-3">
            <div class="flex items-center gap-2 text-cyan-400 font-bold text-sm">
              <span class="w-2.5 h-2.5 rounded-full bg-cyan-400 animate-ping"></span>
              <span>New Verified Alert!</span>
            </div>
            <button (click)="recentBroadcast.set(null)" class="text-slate-400 hover:text-white text-xs p-1">✕</button>
          </div>
          <p class="mt-1 font-semibold text-white text-sm line-clamp-1">{{ liveAlert.extractedLocationName }}</p>
          <div class="mt-2 space-y-2">
            <div class="flex items-center gap-2">
              <app-depth-indicator [depthCategory]="liveAlert.depthCategory" [inches]="liveAlert.estimatedDepthInches" />
              <button
                (click)="focusOnReport(liveAlert)"
                class="ml-auto text-xs font-semibold text-cyan-400 hover:underline"
              >
                View on Map →
              </button>
            </div>
            <app-passability-badge
              [motorcycle]="liveAlert.passableMotorcycle"
              [sedan]="liveAlert.passableSedan"
              [suv]="liveAlert.passableSuvJeepney"
              [selectedVehicle]="selectedVehicle()"
            />
          </div>
        </div>
      }

      <!-- Desktop Quick Info Floating Card on Left -->
      <div class="hidden lg:block absolute bottom-6 left-6 z-20 w-80 p-4 rounded-2xl bg-slate-900/90 backdrop-blur-md border border-slate-800 shadow-xl text-xs">
        <h3 class="font-bold text-white text-sm mb-1 flex items-center gap-1.5">
          <span>🚦</span>
          <span>Passability Legend ({{ selectedVehicle() }})</span>
        </h3>
        <p class="text-slate-400 mb-3">Street pins dynamically re-color based on your selected vehicle's water clearance.</p>
        <div class="space-y-1.5">
          <div class="flex items-center gap-2">
            <span class="w-3 h-3 rounded-full bg-emerald-500 shadow-sm shadow-emerald-500/50"></span>
            <span class="text-slate-200">Safe / Passable for {{ selectedVehicle() }}</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="w-3 h-3 rounded-full bg-amber-500 shadow-sm shadow-amber-500/50"></span>
            <span class="text-slate-200">Caution (Tire-deep water level)</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="w-3 h-3 rounded-full bg-rose-500 shadow-sm shadow-rose-500/50"></span>
            <span class="text-slate-200">Hazard / Submerged (Do not enter)</span>
          </div>
        </div>
      </div>

      <!-- Report Flood Incident Modal -->
      @if (isReportModalOpen()) {
        <app-report-incident
          [currentLat]="geoService.currentCoords().lat"
          [currentLng]="geoService.currentCoords().lng"
          (close)="isReportModalOpen.set(false)"
          (reportCreated)="onNewReportCreated($event)"
        />
      }
    </div>
  `
})
export class LiveMapComponent implements OnInit {
  private readonly mapContainer = viewChild.required<ElementRef<HTMLDivElement>>('mapContainer');
  private readonly apiService = inject(FloodApiService);
  private readonly destroyRef = inject(DestroyRef);
  public readonly signalrService = inject(SignalRService);
  public readonly geoService = inject(GeolocationService);

  private map: L.Map | null = null;
  private markersLayer: L.LayerGroup | null = null;

  public readonly selectedVehicle = signal<VehicleType>('All');
  public readonly isReportModalOpen = signal<boolean>(false);
  public readonly showMobileLegend = signal<boolean>(false);
  public readonly activeReports = signal<FloodReport[]>([]);
  public readonly recentBroadcast = signal<FloodReport | null>(null);

  ngOnInit(): void {
    // Listen for live SignalR flood pushes
    this.signalrService.newReport$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((report) => {
        this.handleLiveReportPushed(report);
      });
  }

  ngAfterViewInit(): void {
    this.initMap();
    this.loadNearbyReports();
  }

  private initMap(): void {
    const coords = this.geoService.currentCoords();

    this.map = L.map(this.mapContainer().nativeElement, {
      center: [coords.lat, coords.lng],
      zoom: 14,
      zoomControl: false
    });

    // Add Zoom Control at bottom left so it doesn't overlap bottom right FABs
    L.control.zoom({ position: 'bottomleft' }).addTo(this.map);

    // Free OpenStreetMap Tile Layer for Metro Manila
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors • BahaGuard AI'
    }).addTo(this.map);

    this.markersLayer = L.layerGroup().addTo(this.map);

    // Commuter current GPS blue marker
    this.addCommuterGpsMarker(coords.lat, coords.lng);
  }

  private addCommuterGpsMarker(lat: number, lng: number): void {
    if (!this.map) return;

    const gpsHtml = `
      <div class="relative flex items-center justify-center">
        <div class="absolute w-8 h-8 rounded-full bg-blue-500 animate-ping opacity-50"></div>
        <div class="relative w-5 h-5 rounded-full bg-blue-600 border-2 border-white shadow-xl flex items-center justify-center">
          <div class="w-2 h-2 rounded-full bg-white"></div>
        </div>
      </div>
    `;

    const gpsIcon = L.divIcon({
      html: gpsHtml,
      className: 'commuter-gps-pin',
      iconSize: [20, 20],
      iconAnchor: [10, 10]
    });

    L.marker([lat, lng], { icon: gpsIcon, title: 'Your Current Location' })
      .addTo(this.map)
      .bindPopup('<b class="text-slate-900 text-xs">📍 Your GPS Location</b>');
  }

  public recenterToGps(): void {
    this.geoService.requestCurrentPosition();
    const coords = this.geoService.currentCoords();
    if (this.map) {
      this.map.flyTo([coords.lat, coords.lng], 16, { duration: 1.2 });
    }
  }

  public loadNearbyReports(): void {
    const coords = this.geoService.currentCoords();

    this.apiService
      .getNearbyReports({
        lat: coords.lat,
        lng: coords.lng,
        radiusMeters: 5000,
        vehicleType: this.selectedVehicle()
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (reports) => {
          this.activeReports.set(reports);
          this.renderMarkers();
        },
        error: (err) => {
          console.error('Failed to fetch nearby flood reports:', err);
        }
      });
  }

  public setVehicleFilter(vehicle: VehicleType): void {
    this.selectedVehicle.set(vehicle);
    this.renderMarkers();
  }

  public jumpToPreset(preset: LandmarkPreset): void {
    this.geoService.selectPreset(preset);
    if (this.map) {
      this.map.flyTo([preset.lat, preset.lng], 15, { duration: 1.2 });
    }
    this.loadNearbyReports();
  }

  public focusOnReport(report: FloodReport): void {
    if (this.map) {
      this.map.flyTo([report.latitude, report.longitude], 16, { duration: 1 });
    }
  }

  private handleLiveReportPushed(report: FloodReport): void {
    console.log('Real-time Pin Dropped on Live Map:', report);
    this.recentBroadcast.set(report);

    // Prepend to active reports list
    const current = this.activeReports();
    this.activeReports.set([report, ...current.filter((r) => r.id !== report.id)]);

    // Re-render map pins
    this.renderMarkers();

    // Smoothly pan to the new incident
    if (this.map) {
      this.map.panTo([report.latitude, report.longitude]);
    }
  }

  public onNewReportCreated(report: FloodReport): void {
    this.isReportModalOpen.set(false);
    this.handleLiveReportPushed(report);
  }

  private renderMarkers(): void {
    if (!this.markersLayer) return;

    this.markersLayer.clearLayers();
    const reports = this.activeReports();
    const vehicle = this.selectedVehicle();

    reports.forEach((report) => {
      const pinColor = this.getPinColor(report, vehicle);
      const icon = this.createPinIcon(pinColor, report.depthCategory);

      const marker = L.marker([report.latitude, report.longitude], { icon })
        .bindPopup(this.createPopupContent(report), {
          className: 'flood-popup',
          maxWidth: typeof window !== 'undefined' ? Math.min(window.innerWidth - 48, 320) : 300
        });

      this.markersLayer?.addLayer(marker);
    });
  }

  private getPinColor(report: FloodReport, vehicle: VehicleType): 'green' | 'amber' | 'red' {
    if (vehicle === 'Motorcycle') {
      return report.passableMotorcycle ? 'green' : 'red';
    }
    if (vehicle === 'Sedan') {
      return report.passableSedan ? 'green' : 'red';
    }
    if (vehicle === 'Suv') {
      return report.passableSuvJeepney ? 'green' : 'red';
    }

    // Default 'All' view: based on depth severity
    switch (report.depthCategory) {
      case 'Gutter': return 'green';
      case 'HalfTire': return 'amber';
      default: return 'red';
    }
  }

  private createPinIcon(color: 'green' | 'amber' | 'red', depth: string): L.DivIcon {
    const bg = color === 'green' ? 'bg-emerald-500' : color === 'amber' ? 'bg-amber-500' : 'bg-rose-600';
    const ping = color === 'green' ? 'bg-emerald-400' : color === 'amber' ? 'bg-amber-400' : 'bg-rose-500';

    const html = `
      <div class="relative flex items-center justify-center cursor-pointer transform hover:scale-110 transition">
        <div class="absolute w-8 h-8 rounded-full ${ping} animate-ping opacity-60"></div>
        <div class="relative w-8 h-8 rounded-2xl ${bg} border-2 border-white shadow-xl flex items-center justify-center text-white text-xs font-black">
          ${depth === 'Gutter' ? '4"' : depth === 'HalfTire' ? '10"' : depth === 'Knee' ? '15"' : '⚠️'}
        </div>
      </div>
    `;

    return L.divIcon({
      html,
      className: 'live-flood-pin',
      iconSize: [32, 32],
      iconAnchor: [16, 16],
      popupAnchor: [0, -18]
    });
  }

  private createPopupContent(report: FloodReport): string {
    const photoTag = report.imageUrl
      ? `<div class="w-full h-32 overflow-hidden rounded-t-xl bg-slate-950">
           <img src="${report.imageUrl}" alt="Flood incident" class="w-full h-full object-cover">
         </div>`
      : '';

    return `
      <div class="p-4 bg-slate-900 text-slate-100 rounded-2xl text-xs max-w-xs">
        ${photoTag}
        <div class="mt-2 flex items-center justify-between">
          <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase bg-slate-800 text-cyan-400 border border-slate-700">
            ${report.depthCategory} (${report.estimatedDepthInches}")
          </span>
          <span class="text-slate-400 text-[10px]">AI Conf: ${Math.round(report.aiConfidenceScore * 100)}%</span>
        </div>

        <h4 class="mt-2 font-bold text-white text-sm leading-snug">${report.extractedLocationName}</h4>
        <p class="mt-1 text-slate-300 italic">"${report.rawDescription}"</p>

        <div class="mt-3 pt-2 border-t border-slate-800 space-y-1">
          <div class="flex justify-between items-center text-[11px]">
            <span>🏍️ Motorcycle:</span>
            <span class="${report.passableMotorcycle ? 'text-emerald-400 font-bold' : 'text-rose-400 font-bold'}">
              ${report.passableMotorcycle ? 'Passable' : 'Hazard'}
            </span>
          </div>
          <div class="flex justify-between items-center text-[11px]">
            <span>🚗 Sedan:</span>
            <span class="${report.passableSedan ? 'text-emerald-400 font-bold' : 'text-rose-400 font-bold'}">
              ${report.passableSedan ? 'Passable' : 'Submerged'}
            </span>
          </div>
          <div class="flex justify-between items-center text-[11px]">
            <span>🚙 SUV / Jeepney:</span>
            <span class="${report.passableSuvJeepney ? 'text-emerald-400 font-bold' : 'text-rose-400 font-bold'}">
              ${report.passableSuvJeepney ? 'Passable' : 'Submerged'}
            </span>
          </div>
        </div>
      </div>
    `;
  }
}
