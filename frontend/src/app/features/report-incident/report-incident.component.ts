import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  inject,
  input,
  OnInit,
  output,
  signal
} from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FloodReport } from '../../core/models/flood-report.model';
import { FloodApiService } from '../../core/services/flood-api.service';

@Component({
  selector: 'app-report-incident',
  standalone: true,
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md overflow-y-auto"
      (click)="onBackdropClick($event)"
      (keydown.escape)="close.emit()"
      tabindex="-1"
    >
      <div
        class="relative w-full max-w-lg bg-slate-900 border border-slate-700/80 rounded-3xl shadow-2xl overflow-hidden text-slate-100 my-8 animate-in fade-in zoom-in-95 duration-200"
        (click)="$event.stopPropagation()"
      >
        <!-- Modal Header -->
        <div class="px-6 py-5 bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 border-b border-slate-800 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-500 to-blue-600 flex items-center justify-center text-xl shadow-lg shadow-cyan-500/20">
              📸
            </div>
            <div>
              <h2 class="text-lg font-bold text-white tracking-tight flex items-center gap-2">
                Report Live Flood
                <span class="px-2 py-0.5 text-[10px] uppercase font-extrabold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
                  AI Calibrated
                </span>
              </h2>
              <p class="text-xs text-slate-400">Crowdsourced & analyzed via Gemini Multimodal Vision</p>
            </div>
          </div>
          <button
            type="button"
            (click)="close.emit()"
            class="w-8 h-8 rounded-full bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white flex items-center justify-center transition border border-slate-700"
            aria-label="Close dialog"
          >
            ✕
          </button>
        </div>

        <!-- Form Body -->
        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="p-6 space-y-4">
          @if (errorMessage()) {
            <div class="p-3 rounded-xl bg-rose-500/10 border border-rose-500/30 text-rose-400 text-xs flex items-center gap-2">
              <span>⚠️</span>
              <span>{{ errorMessage() }}</span>
            </div>
          }

          <!-- Taglish Description Input -->
          <div>
            <label for="rawDescription" class="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1.5">
              Incident Description (Taglish / English) <span class="text-cyan-400">*</span>
            </label>
            <textarea
              id="rawDescription"
              formControlName="rawDescription"
              rows="3"
              placeholder="e.g. Hanggang binti na sa may tapat ng UST Dapitan, baha na at di na makadaan ang motor..."
              class="w-full px-3.5 py-2.5 rounded-xl bg-slate-950 border border-slate-700 focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 text-slate-100 placeholder-slate-500 text-xs resize-none outline-none transition"
            ></textarea>
            @if (form.controls.rawDescription.touched && form.controls.rawDescription.invalid) {
              <p class="mt-1 text-[11px] text-rose-400">Please provide a brief description (min 5 characters).</p>
            }

            <!-- Taglish Quick Presets -->
            <div class="mt-2">
              <span class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Quick suggestions:</span>
              <div class="mt-1 flex flex-wrap gap-1.5">
                @for (preset of quickPresets; track preset.label) {
                  <button
                    type="button"
                    (click)="applyPreset(preset.text)"
                    class="px-2 py-0.5 rounded-lg text-[10px] bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-cyan-300 border border-slate-700/60 transition"
                  >
                    {{ preset.label }}
                  </button>
                }
              </div>
            </div>
          </div>

          <!-- Photo Upload & Vision Preview -->
          <div>
            <label class="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1.5">
              Photo Evidence <span class="text-slate-500 font-normal">(Optional - Triggers Vision AI Calibration)</span>
            </label>

            @if (previewUrl()) {
              <div class="relative rounded-2xl overflow-hidden border border-cyan-500/40 bg-slate-950 max-h-48 group">
                <img [src]="previewUrl()" alt="Flood preview" class="w-full h-44 object-cover">
                <div class="absolute inset-0 bg-slate-950/60 opacity-0 group-hover:opacity-100 transition flex items-center justify-center gap-3">
                  <button
                    type="button"
                    (click)="removePhoto()"
                    class="px-3 py-1.5 rounded-xl bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold shadow-lg transition"
                  >
                    Remove Photo
                  </button>
                </div>
                <div class="absolute bottom-2 left-2 px-2 py-0.5 rounded bg-slate-900/80 backdrop-blur-sm text-[10px] font-mono text-cyan-300 border border-cyan-500/30">
                  Ready for Multimodal Depth Audit
                </div>
              </div>
            } @else {
              <label
                for="photoInput"
                class="flex flex-col items-center justify-center p-4 border-2 border-dashed border-slate-700 hover:border-cyan-500/60 rounded-2xl cursor-pointer bg-slate-950/50 hover:bg-slate-950 transition text-center"
              >
                <span class="text-2xl mb-1">📷</span>
                <span class="text-xs font-medium text-slate-300">Click to upload street photo or screenshot</span>
                <span class="text-[10px] text-slate-500 mt-0.5">JPG, PNG, WEBP up to 10MB</span>
                <input
                  id="photoInput"
                  type="file"
                  accept="image/jpeg,image/png,image/webp"
                  (change)="onFileSelected($event)"
                  class="hidden"
                />
              </label>
            }
          </div>

          <!-- Location & City Grid -->
          <div class="grid grid-cols-2 gap-3">
            <div>
              <label for="citySelect" class="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1.5">
                City / LGU <span class="text-cyan-400">*</span>
              </label>
              <select
                id="citySelect"
                formControlName="city"
                class="w-full px-3 py-2 rounded-xl bg-slate-950 border border-slate-700 text-slate-100 text-xs focus:border-cyan-500 outline-none"
              >
                @for (c of cities; track c) {
                  <option [value]="c">{{ c }}</option>
                }
              </select>
            </div>

            <div>
              <label for="barangayInput" class="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1.5">
                Barangay / Landmark
              </label>
              <input
                id="barangayInput"
                type="text"
                formControlName="barangay"
                placeholder="e.g. Brgy. 471, Sampaloc"
                class="w-full px-3 py-2 rounded-xl bg-slate-950 border border-slate-700 text-slate-100 text-xs focus:border-cyan-500 outline-none placeholder-slate-500"
              />
            </div>
          </div>

          <!-- GPS Coordinates -->
          <div class="grid grid-cols-2 gap-3 pt-1">
            <div>
              <label for="latitudeInput" class="block text-[11px] font-mono text-slate-400 mb-1">
                Latitude:
              </label>
              <input
                id="latitudeInput"
                type="number"
                step="any"
                formControlName="latitude"
                class="w-full px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-slate-300 font-mono text-xs focus:border-cyan-500 outline-none"
              />
            </div>
            <div>
              <label for="longitudeInput" class="block text-[11px] font-mono text-slate-400 mb-1">
                Longitude:
              </label>
              <input
                id="longitudeInput"
                type="number"
                step="any"
                formControlName="longitude"
                class="w-full px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-slate-300 font-mono text-xs focus:border-cyan-500 outline-none"
              />
            </div>
          </div>

          <!-- Reporter Alias -->
          <div>
            <label for="reporterAlias" class="block text-xs font-semibold uppercase tracking-wider text-slate-300 mb-1">
              Reporter Alias <span class="text-slate-500 font-normal">(Optional)</span>
            </label>
            <input
              id="reporterAlias"
              type="text"
              formControlName="reporterAlias"
              placeholder="e.g. España Commuter or Angkas Rider"
              class="w-full px-3 py-2 rounded-xl bg-slate-950 border border-slate-700 text-slate-100 text-xs focus:border-cyan-500 outline-none placeholder-slate-500"
            />
          </div>

          <!-- Actions -->
          <div class="pt-3 border-t border-slate-800 flex items-center justify-end gap-3">
            <button
              type="button"
              (click)="close.emit()"
              [disabled]="isSubmitting()"
              class="px-4 py-2 rounded-xl text-xs font-medium text-slate-400 hover:text-white hover:bg-slate-800 transition"
            >
              Cancel
            </button>
            <button
              type="submit"
              [disabled]="form.invalid || isSubmitting()"
              class="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-cyan-500 to-blue-600 hover:from-cyan-400 hover:to-blue-500 disabled:from-slate-700 disabled:to-slate-800 disabled:text-slate-500 text-white font-bold text-xs shadow-lg shadow-cyan-500/25 transition active:scale-95"
            >
              @if (isSubmitting()) {
                <span class="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin"></span>
                <span>Submitting & Analyzing...</span>
              } @else {
                <span>🚀</span>
                <span>Submit & Publish Pin</span>
              }
            </button>
          </div>
        </form>
      </div>
    </div>
  `
})
export class ReportIncidentComponent implements OnInit {
  private readonly apiService = inject(FloodApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly currentLat = input<number>(14.5995);
  readonly currentLng = input<number>(120.9842);

  readonly close = output<void>();
  readonly reportCreated = output<FloodReport>();

  readonly isSubmitting = signal<boolean>(false);
  readonly errorMessage = signal<string | null>(null);
  readonly selectedFile = signal<File | null>(null);
  readonly previewUrl = signal<string | null>(null);

  readonly cities: string[] = [
    'Manila',
    'Quezon City',
    'Pasay',
    'Makati',
    'Mandaluyong',
    'San Juan',
    'Taguig',
    'Pasig',
    'Caloocan',
    'Malabon',
    'Navotas',
    'Valenzuela',
    'Marikina',
    'Parañaque',
    'Las Piñas',
    'Muntinlupa'
  ];

  readonly quickPresets = [
    { label: 'UST España', text: 'Hanggang binti na sa may UST España tapat ng UST Main, lubog ang sedan at motor.' },
    { label: 'Taft Kalaw', text: 'Taft corner T.M. Kalaw abot gutter na ang baha, mabagal na ang usad ng mga sasakyan.' },
    { label: 'Rizal Blumentritt', text: 'Lagpas gulong na ang baha sa Rizal Ave Blumentritt, impassable na sa kahit anong kotse.' },
    { label: 'EDSA Megamall', text: 'May naipong baha sa may ilalim ng flyover EDSA Ortigas, passable pa sa mga SUV.' }
  ];

  readonly form = new FormGroup({
    rawDescription: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(5)]
    }),
    reporterAlias: new FormControl('Metro Commuter', { nonNullable: true }),
    barangay: new FormControl('', { nonNullable: true }),
    city: new FormControl('Manila', {
      nonNullable: true,
      validators: [Validators.required]
    }),
    latitude: new FormControl<number>(14.5995, {
      nonNullable: true,
      validators: [Validators.required]
    }),
    longitude: new FormControl<number>(120.9842, {
      nonNullable: true,
      validators: [Validators.required]
    })
  });

  constructor() {
    // Keep form coordinates aligned when parent signals change
    effect(() => {
      const lat = this.currentLat();
      const lng = this.currentLng();
      this.form.patchValue({ latitude: lat, longitude: lng });
    });
  }

  ngOnInit(): void {
    this.form.patchValue({
      latitude: this.currentLat(),
      longitude: this.currentLng()
    });
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.close.emit();
    }
  }

  applyPreset(text: string): void {
    this.form.patchValue({ rawDescription: text });
  }

  onFileSelected(event: Event): void {
    const inputEl = event.target as HTMLInputElement;
    if (inputEl.files && inputEl.files.length > 0) {
      const file = inputEl.files[0];

      // Validate file size (< 10MB)
      if (file.size > 10 * 1024 * 1024) {
        this.errorMessage.set('Image file exceeds the 10MB limit.');
        return;
      }

      this.selectedFile.set(file);
      this.errorMessage.set(null);

      const reader = new FileReader();
      reader.onload = () => {
        this.previewUrl.set(reader.result as string);
      };
      reader.readAsDataURL(file);
    }
  }

  removePhoto(): void {
    this.selectedFile.set(null);
    this.previewUrl.set(null);
  }

  onSubmit(): void {
    if (this.form.invalid || this.isSubmitting()) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);

    const val = this.form.getRawValue();

    this.apiService
      .submitReport({
        rawDescription: val.rawDescription,
        latitude: val.latitude,
        longitude: val.longitude,
        reporterAlias: val.reporterAlias,
        barangay: val.barangay || undefined,
        city: val.city,
        photo: this.selectedFile() ?? undefined
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (createdReport) => {
          this.isSubmitting.set(false);
          this.reportCreated.emit(createdReport);
          this.close.emit();
        },
        error: (err) => {
          this.isSubmitting.set(false);
          console.error('Error submitting report:', err);
          const detail = err.error?.detail || err.error?.title || 'Failed to submit flood report. Please try again.';
          this.errorMessage.set(detail);
        }
      });
  }
}
