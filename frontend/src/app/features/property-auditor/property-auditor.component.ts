import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  signal
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DatePipe, DecimalPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PropertyAuditResponse } from '../../core/models/flood-report.model';
import { FloodApiService } from '../../core/services/flood-api.service';

@Component({
  selector: 'app-property-auditor',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 font-sans">
      <!-- Top Navigation -->
      <header class="px-4 sm:px-6 py-3.5 sm:py-4 bg-slate-900/90 backdrop-blur-md border-b border-slate-800 flex items-center justify-between">
        <div class="flex items-center gap-2.5 sm:gap-3">
          <a routerLink="/" class="flex items-center gap-2 text-white hover:text-cyan-400 transition">
            <div class="w-8 h-8 sm:w-9 sm:h-9 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-500 flex items-center justify-center shadow-lg shadow-cyan-500/20">
              <span class="text-lg sm:text-xl">🌊</span>
            </div>
            <div>
              <span class="font-extrabold text-sm sm:text-base tracking-tight">BahaGuard AI</span>
              <span class="ml-1.5 sm:ml-2 px-1.5 py-0.5 text-[9px] sm:text-[10px] font-bold uppercase rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
                Auditor
              </span>
            </div>
          </a>
        </div>

        <div class="flex items-center gap-2 sm:gap-3">
          <a
            routerLink="/"
            class="px-3 py-1.5 sm:px-3.5 sm:py-1.5 rounded-xl bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 transition border border-slate-700 flex items-center gap-1.5 active:scale-95"
          >
            <span>🗺️</span>
            <span>Live Map</span>
          </a>
        </div>
      </header>

      <!-- Main Audit Hero & Search -->
      <main class="max-w-4xl mx-auto px-3 sm:px-4 py-6 sm:py-8 space-y-5 sm:space-y-6">
        <div class="text-center space-y-2 max-w-xl mx-auto">
          <div class="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/30 text-cyan-400 text-xs font-semibold">
            <span>🛡️</span>
            <span>Spatial Tenant & Dorm Due Diligence</span>
          </div>
          <h1 class="text-2xl sm:text-3xl font-extrabold text-white tracking-tight">
            Property Flood-Risk Auditor
          </h1>
          <p class="text-xs sm:text-sm text-slate-400">
            Audit any apartment, condo, student dorm, or property in Metro Manila against spatial flood records before signing a lease.
          </p>
        </div>

        <!-- Search Bar -->
        <div class="p-4 sm:p-6 rounded-3xl bg-slate-900 border border-slate-800 shadow-2xl space-y-3.5 sm:space-y-4">
          <form [formGroup]="auditForm" (ngSubmit)="runAudit()" class="flex flex-col sm:flex-row gap-2.5 sm:gap-3">
            <div class="flex-1 relative">
              <span class="absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-500">📍</span>
              <input
                type="text"
                formControlName="address"
                placeholder="Enter address or landmark (e.g. 839 Lacson Ave, Sampaloc, Manila)"
                class="w-full pl-10 pr-4 py-3 rounded-2xl bg-slate-950 border border-slate-700 focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 text-slate-100 placeholder-slate-500 text-sm sm:text-xs outline-none transition"
              />
            </div>
            <button
              type="submit"
              [disabled]="auditForm.invalid || isLoading()"
              class="px-6 py-3 rounded-2xl bg-gradient-to-r from-cyan-500 to-blue-600 hover:from-cyan-400 hover:to-blue-500 disabled:from-slate-700 disabled:to-slate-800 disabled:text-slate-500 text-white font-bold text-xs shadow-lg shadow-cyan-500/25 transition active:scale-95 flex items-center justify-center gap-2"
            >
              @if (isLoading()) {
                <span class="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
                <span>Auditing Spatial Database...</span>
              } @else {
                <span>🔍</span>
                <span>Audit Property</span>
              }
            </button>
          </form>

          <!-- Quick Landmark Chips (Scrollable on Mobile) -->
          <div class="flex items-center gap-1.5 pt-1 overflow-x-auto no-scrollbar pb-1">
            <span class="text-[11px] text-slate-400 font-semibold mr-1 whitespace-nowrap">Popular Hotspots:</span>
            @for (area of sampleLocations; track area.name) {
              <button
                type="button"
                (click)="selectSample(area.address)"
                class="whitespace-nowrap flex-shrink-0 px-2.5 py-1 rounded-xl text-xs bg-slate-800/80 hover:bg-slate-700 text-slate-300 hover:text-cyan-400 border border-slate-700/60 transition active:scale-95"
              >
                {{ area.name }}
              </button>
            }
          </div>
        </div>

        <!-- Audit Result Card -->
        @if (currentResult(); as res) {
          <div class="p-4 sm:p-6 rounded-3xl bg-slate-900 border border-slate-800 shadow-xl space-y-5 sm:space-y-6 animate-in fade-in slide-in-from-bottom-4 duration-300">
            <!-- Header & Score -->
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 sm:gap-4 pb-4 border-b border-slate-800">
              <div>
                <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Audited Location</span>
                <h3 class="text-base sm:text-lg font-bold text-white mt-0.5">{{ res.resolvedLocationName }}</h3>
                <p class="text-xs text-cyan-400 font-mono mt-0.5">GPS: {{ res.latitude | number:'1.4-4' }}, {{ res.longitude | number:'1.4-4' }}</p>
              </div>

              <!-- Risk Score Gauge -->
              <div class="flex items-center justify-between sm:justify-end gap-3 pt-2 sm:pt-0">
                <div class="text-left sm:text-right">
                  <div class="text-2xl font-black"
                    [class]="res.riskScore >= 7 ? 'text-rose-400' : res.riskScore >= 4 ? 'text-amber-400' : 'text-emerald-400'">
                    {{ res.riskScore }}<span class="text-xs text-slate-500 font-normal">/10</span>
                  </div>
                  <span class="text-[10px] text-slate-400 uppercase font-semibold">Flood Risk Score</span>
                </div>
                <span
                  [class]="res.riskLevel === 'Extreme' || res.riskLevel === 'High'
                    ? 'bg-rose-500/20 text-rose-400 border-rose-500/40'
                    : res.riskLevel === 'Moderate'
                    ? 'bg-amber-500/20 text-amber-400 border-amber-500/40'
                    : 'bg-emerald-500/20 text-emerald-400 border-emerald-500/40'"
                  class="px-3.5 py-2 rounded-xl text-xs font-extrabold uppercase tracking-wider border shadow-sm flex items-center gap-1.5"
                >
                  <span class="w-2 h-2 rounded-full animate-ping"
                    [class]="res.riskLevel === 'Extreme' || res.riskLevel === 'High' ? 'bg-rose-400' : res.riskLevel === 'Moderate' ? 'bg-amber-400' : 'bg-emerald-400'">
                  </span>
                  <span>{{ res.riskLevel }} Risk</span>
                </span>
              </div>
            </div>

            <!-- Spatial Metrics Grid -->
            <div class="grid grid-cols-2 sm:grid-cols-4 gap-2.5 sm:gap-3">
              <div class="p-3 sm:p-3.5 rounded-2xl bg-slate-950/70 border border-slate-800/80">
                <span class="text-[10px] uppercase font-semibold text-slate-400">Historical Events</span>
                <p class="text-lg sm:text-xl font-black text-white mt-0.5">{{ res.historicalIncidentsCount }}</p>
                <span class="text-[10px] text-slate-500">In 500m radius</span>
              </div>

              <div class="p-3 sm:p-3.5 rounded-2xl bg-slate-950/70 border border-slate-800/80">
                <span class="text-[10px] uppercase font-semibold text-slate-400">Average Depth</span>
                <p class="text-lg sm:text-xl font-black text-amber-400 mt-0.5">{{ res.averageDepthInches }}"</p>
                <span class="text-[10px] text-slate-500">{{ res.predominantDepthCategory }} level</span>
              </div>

              <div class="p-3 sm:p-3.5 rounded-2xl bg-slate-950/70 border border-slate-800/80">
                <span class="text-[10px] uppercase font-semibold text-slate-400">Max Recorded</span>
                <p class="text-lg sm:text-xl font-black text-rose-400 mt-0.5">{{ res.maxDepthInches }}"</p>
                <span class="text-[10px] text-slate-500">Typhoon peak</span>
              </div>

              <div class="p-3 sm:p-3.5 rounded-2xl bg-slate-950/70 border border-slate-800/80">
                <span class="text-[10px] uppercase font-semibold text-slate-400">Receding Time</span>
                <p class="text-lg sm:text-xl font-black text-cyan-400 mt-0.5">~{{ res.estimatedRecedingTimeHours }} hrs</p>
                <span class="text-[10px] text-slate-500">Subsidence duration</span>
              </div>
            </div>

            <!-- Vehicle Vulnerability Matrix -->
            <div class="p-3.5 sm:p-4 rounded-2xl bg-slate-950/40 border border-slate-800 space-y-2.5">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                <span class="flex items-center gap-1.5 text-xs font-bold text-slate-200 uppercase tracking-wider">
                  <span>🚦</span>
                  <span>Vehicle Passability Vulnerability</span>
                </span>
                <div class="flex flex-wrap items-center gap-1.5">
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                    [class]="res.vehicleVulnerability.motorcycleHighRisk ? 'bg-rose-500/20 text-rose-400' : 'bg-emerald-500/20 text-emerald-400'">
                    🏍️ Moto: {{ res.vehicleVulnerability.motorcycleHighRisk ? 'Hazard' : 'Passable' }}
                  </span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                    [class]="res.vehicleVulnerability.sedanHighRisk ? 'bg-rose-500/20 text-rose-400' : 'bg-emerald-500/20 text-emerald-400'">
                    🚗 Sedan: {{ res.vehicleVulnerability.sedanHighRisk ? 'Submerged' : 'Passable' }}
                  </span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                    [class]="res.vehicleVulnerability.suvHighRisk ? 'bg-rose-500/20 text-rose-400' : 'bg-emerald-500/20 text-emerald-400'">
                    🚙 SUV: {{ res.vehicleVulnerability.suvHighRisk ? 'Hazard' : 'Passable' }}
                  </span>
                </div>
              </div>
              <p class="text-xs text-slate-300 leading-relaxed">{{ res.vehicleVulnerability.parkingAdvisory }}</p>
            </div>

            <!-- Gemini AI Tenant Due Diligence Breakdown -->
            <div class="p-4 sm:p-5 rounded-2xl bg-cyan-950/20 border border-cyan-800/40 space-y-3.5 sm:space-y-4">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-1">
                <div class="flex items-center gap-2 text-cyan-300 font-bold text-xs uppercase tracking-wider">
                  <span>✨</span>
                  <span>Gemini 2.5 Flash Due-Diligence Synthesis</span>
                </div>
                <span class="text-[10px] text-slate-400">Audited at {{ res.auditedAt | date:'short' }}</span>
              </div>

              <!-- Executive Summary -->
              <div class="p-3 rounded-xl bg-slate-900/80 border border-slate-800 text-xs">
                <span class="text-[10px] uppercase font-bold text-cyan-400 tracking-wider">Executive Verdict</span>
                <p class="text-white font-medium mt-0.5 leading-relaxed">{{ res.dueDiligenceReport.executiveSummary }}</p>
              </div>

              <!-- 3-Column Advisory -->
              <div class="grid grid-cols-1 md:grid-cols-3 gap-3 text-xs">
                <div class="p-3 rounded-xl bg-slate-900/60 border border-slate-800/80">
                  <span class="text-[10px] uppercase font-bold text-amber-400 tracking-wider flex items-center gap-1">
                    <span>🏢</span> Ground-Floor Advice
                  </span>
                  <p class="text-slate-300 mt-1 leading-relaxed">{{ res.dueDiligenceReport.groundFloorRiskAdvice }}</p>
                </div>

                <div class="p-3 rounded-xl bg-slate-900/60 border border-slate-800/80">
                  <span class="text-[10px] uppercase font-bold text-cyan-400 tracking-wider flex items-center gap-1">
                    <span>⚡</span> Power & Utilities
                  </span>
                  <p class="text-slate-300 mt-1 leading-relaxed">{{ res.dueDiligenceReport.electricalAndUtilityRisk }}</p>
                </div>

                <div class="p-3 rounded-xl bg-slate-900/60 border border-slate-800/80">
                  <span class="text-[10px] uppercase font-bold text-emerald-400 tracking-wider flex items-center gap-1">
                    <span>🚶</span> Evacuation & Transit
                  </span>
                  <p class="text-slate-300 mt-1 leading-relaxed">{{ res.dueDiligenceReport.evacuationAndAccessAdvisory }}</p>
                </div>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class PropertyAuditorComponent {
  private readonly apiService = inject(FloodApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly isLoading = signal<boolean>(false);
  readonly currentResult = signal<PropertyAuditResponse | null>(null);

  readonly auditForm = new FormGroup({
    address: new FormControl('España Blvd corner Lacson Ave, Sampaloc, Manila', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3)]
    })
  });

  readonly sampleLocations = [
    { name: 'UST España', address: 'España Blvd corner Lacson Ave, Sampaloc, Manila' },
    { name: 'DLSU Taft', address: '2401 Taft Ave, Malate, Manila' },
    { name: 'PUP Santa Mesa', address: 'Anonas St, Santa Mesa, Manila' },
    { name: 'Araneta Ave QC', address: 'Araneta Ave corner E. Rodriguez, Quezon City' },
    { name: 'Quiapo Church', address: 'Quezon Blvd, Quiapo, Manila' }
  ];

  selectSample(address: string): void {
    this.auditForm.patchValue({ address });
    this.runAudit();
  }

  runAudit(): void {
    if (this.auditForm.invalid) return;

    this.isLoading.set(true);
    const address = this.auditForm.getRawValue().address;

    this.apiService
      .getPropertyAudit(address)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.isLoading.set(false);
          this.currentResult.set(result);
        },
        error: (err) => {
          this.isLoading.set(false);
          console.error('Audit query error:', err);
        }
      });
  }
}
