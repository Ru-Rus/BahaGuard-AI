import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { VehicleType } from '../../core/models/flood-report.model';

@Component({
  selector: 'app-passability-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-wrap gap-1.5 text-xs font-medium">
      <!-- Motorcycle Badge -->
      <span
        [class]="motorcycle()
          ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
          : 'bg-rose-500/20 text-rose-400 border border-rose-500/30'"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full"
      >
        <span class="text-sm">🏍️</span>
        <span>Moto: {{ motorcycle() ? 'Passable' : 'Submerged' }}</span>
      </span>

      <!-- Sedan Badge -->
      <span
        [class]="sedan()
          ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
          : 'bg-rose-500/20 text-rose-400 border border-rose-500/30'"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full"
      >
        <span class="text-sm">🚗</span>
        <span>Sedan: {{ sedan() ? 'Passable' : 'Submerged' }}</span>
      </span>

      <!-- SUV / Jeepney Badge -->
      <span
        [class]="suv()
          ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30'
          : 'bg-rose-500/20 text-rose-400 border border-rose-500/30'"
        class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full"
      >
        <span class="text-sm">🚙</span>
        <span>SUV/Jeep: {{ suv() ? 'Passable' : 'Hazard' }}</span>
      </span>
    </div>
  `
})
export class PassabilityBadgeComponent {
  readonly motorcycle = input.required<boolean>();
  readonly sedan = input.required<boolean>();
  readonly suv = input.required<boolean>();
  readonly selectedVehicle = input<VehicleType>('All');
}
