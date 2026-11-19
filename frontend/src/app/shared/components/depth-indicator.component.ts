import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DepthCategory } from '../../core/models/flood-report.model';

@Component({
  selector: 'app-depth-indicator',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="inline-flex items-center gap-2">
      <span
        [class]="categoryStyle().badgeClass"
        class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-md text-xs font-semibold uppercase tracking-wider border shadow-sm"
      >
        <span class="w-2 h-2 rounded-full animate-ping" [class]="categoryStyle().dotClass"></span>
        <span>{{ categoryLabel() }} ({{ inches() }}")</span>
      </span>
    </div>
  `
})
export class DepthIndicatorComponent {
  readonly depthCategory = input.required<DepthCategory>();
  readonly inches = input.required<number>();

  readonly categoryLabel = computed(() => {
    switch (this.depthCategory()) {
      case 'Gutter': return 'Gutter Deep';
      case 'HalfTire': return 'Half-Tire';
      case 'Knee': return 'Knee Deep';
      case 'Waist': return 'Waist Deep';
      case 'ChestPlus': return 'Chest Deep+';
      default: return 'Dry';
    }
  });

  readonly categoryStyle = computed(() => {
    switch (this.depthCategory()) {
      case 'Gutter':
        return {
          badgeClass: 'bg-cyan-500/15 text-cyan-400 border-cyan-500/30',
          dotClass: 'bg-cyan-400'
        };
      case 'HalfTire':
        return {
          badgeClass: 'bg-amber-500/15 text-amber-400 border-amber-500/30',
          dotClass: 'bg-amber-400'
        };
      case 'Knee':
        return {
          badgeClass: 'bg-orange-500/15 text-orange-400 border-orange-500/30',
          dotClass: 'bg-orange-400'
        };
      case 'Waist':
        return {
          badgeClass: 'bg-rose-500/15 text-rose-400 border-rose-500/30',
          dotClass: 'bg-rose-400'
        };
      case 'ChestPlus':
        return {
          badgeClass: 'bg-purple-500/15 text-purple-400 border-purple-500/30',
          dotClass: 'bg-purple-400'
        };
      default:
        return {
          badgeClass: 'bg-slate-500/15 text-slate-400 border-slate-500/30',
          dotClass: 'bg-slate-400'
        };
    }
  });
}
