import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FloodReport } from '../models/flood-report.model';

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection: signalR.HubConnection | null = null;
  
  public readonly isConnected = signal<boolean>(false);
  public readonly latestReport = signal<FloodReport | null>(null);
  public readonly newReport$ = new Subject<FloodReport>();

  constructor() {
    this.initConnection();
  }

  private initConnection(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(environment.hubUrl, {
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.registerEventHandlers();
    this.startConnection();
  }

  private registerEventHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('ReceiveNewFloodReport', (report: FloodReport) => {
      console.log('SignalR: Received live flood report:', report);
      this.latestReport.set(report);
      this.newReport$.next(report);
    });

    this.hubConnection.on('ReceiveReportStatusUpdate', (reportId: string, status: string) => {
      console.log(`SignalR: Report ${reportId} updated to ${status}`);
    });

    this.hubConnection.onreconnecting(() => {
      console.warn('SignalR: Reconnecting to flood alerts hub...');
      this.isConnected.set(false);
    });

    this.hubConnection.onreconnected(() => {
      console.log('SignalR: Reconnected to flood alerts hub.');
      this.isConnected.set(true);
      this.subscribeToCityAlerts('manila');
    });

    this.hubConnection.onclose(() => {
      console.warn('SignalR: Connection closed.');
      this.isConnected.set(false);
    });
  }

  private async startConnection(): Promise<void> {
    if (!this.hubConnection) return;

    try {
      await this.hubConnection.start();
      console.log('SignalR: Connected to /hubs/flood-alerts');
      this.isConnected.set(true);
      await this.subscribeToCityAlerts('manila');
    } catch (err) {
      console.warn('SignalR: Initial connection attempt failed, will retry:', err);
      this.isConnected.set(false);
    }
  }

  public async subscribeToCityAlerts(city: string): Promise<void> {
    if (this.hubConnection && this.isConnected()) {
      try {
        await this.hubConnection.invoke('SubscribeToCityAlerts', city);
      } catch (err) {
        console.error(`Failed to subscribe to city ${city}:`, err);
      }
    }
  }
}
