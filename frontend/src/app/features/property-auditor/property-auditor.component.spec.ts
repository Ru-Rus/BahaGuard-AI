import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { PropertyAuditorComponent } from './property-auditor.component';
import { FloodApiService } from '../../core/services/flood-api.service';
import { PropertyAuditResponse } from '../../core/models/flood-report.model';

describe('PropertyAuditorComponent', () => {
  let component: PropertyAuditorComponent;
  let fixture: ComponentFixture<PropertyAuditorComponent>;
  let apiServiceMock: { getPropertyAudit: ReturnType<typeof vi.fn> };

  const mockAuditResult: PropertyAuditResponse = {
    address: 'España Blvd corner Lacson Ave, Sampaloc, Manila',
    resolvedLocationName: 'España Blvd (UST Basin), Sampaloc, Manila',
    latitude: 14.6091,
    longitude: 120.9903,
    riskScore: 8,
    riskLevel: 'High',
    historicalIncidentsCount: 5,
    averageDepthInches: 22.4,
    maxDepthInches: 32,
    predominantDepthCategory: 'Waist',
    estimatedRecedingTimeHours: 4.2,
    vehicleVulnerability: {
      motorcycleHighRisk: true,
      sedanHighRisk: true,
      suvHighRisk: false,
      parkingAdvisory: 'Basement and ground-level street parking at severe water intrusion risk.'
    },
    dueDiligenceReport: {
      executiveSummary: 'High Flood Vulnerability: 5 severe events recorded within 500m.',
      groundFloorRiskAdvice: 'Strong recommendation to choose units on 2nd floor or higher.',
      electricalAndUtilityRisk: 'Confirm emergency standby power is situated above street flood levels.',
      evacuationAndAccessAdvisory: 'Street becomes impassable for sedans and motorcycles during peak Habagat.'
    },
    auditedAt: new Date().toISOString()
  };

  beforeEach(async () => {
    apiServiceMock = {
      getPropertyAudit: vi.fn().mockReturnValue(of(mockAuditResult))
    };

    await TestBed.configureTestingModule({
      imports: [PropertyAuditorComponent],
      providers: [
        provideRouter([]),
        { provide: FloodApiService, useValue: apiServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PropertyAuditorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create the component', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize form with default Manila landmark', () => {
    expect(component.auditForm.get('address')?.value).toContain('España Blvd');
  });

  it('should call getPropertyAudit and render audit metrics when runAudit is executed', async () => {
    component.runAudit();
    fixture.detectChanges();

    expect(apiServiceMock.getPropertyAudit).toHaveBeenCalledWith('España Blvd corner Lacson Ave, Sampaloc, Manila');
    expect(component.currentResult()).toEqual(mockAuditResult);

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('8');
    expect(compiled.textContent).toContain('High Risk');
    expect(compiled.textContent).toContain('22.4"');
    expect(compiled.textContent).toContain('Basement and ground-level street parking at severe water intrusion risk');
  });

  it('should update form and trigger audit when a sample location is clicked', () => {
    component.selectSample('2401 Taft Ave, Malate, Manila');
    expect(component.auditForm.get('address')?.value).toBe('2401 Taft Ave, Malate, Manila');
    expect(apiServiceMock.getPropertyAudit).toHaveBeenCalledWith('2401 Taft Ave, Malate, Manila');
  });
});
