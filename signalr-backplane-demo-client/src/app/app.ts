import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as signalR from '@microsoft/signalr';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  // SignalR connection
  private hubConnection: signalR.HubConnection | null = null;
  
  // UI state
  user = '';
  message = '';
  messages: string[] = [];
  isConnected = false;
  
  // Current server URL (use Docker service names when in containers, localhost for local dev)
  serverUrl = this.getServerUrl(1);

  constructor(private cdr: ChangeDetectorRef) {}

  ngOnInit() {
    this.connectToSignalR();
  }

  ngOnDestroy() {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }

  /**
   * Get the correct server URL based on environment
   */
  getServerUrl(serverNumber: number): string {
    const isLocalhost = typeof window !== 'undefined' && window.location.hostname === 'localhost';
    if (isLocalhost) {
      return `http://localhost:500${serverNumber}`;
    } else {
      return `http://signalr-backplane-demo-server-${serverNumber}:80`;
    }
  }

  /**
   * Connect to SignalR hub
   * This demonstrates the backplane: messages sent to ANY server 
   * will be received by ALL clients via Redis
   */
  connectToSignalR() {
    // Create connection to the current server
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.serverUrl}/chatHub`)
      .build();

    // Listen for messages from ANY server (backplane magic!)
    this.hubConnection.on('ReceiveMessage', (user: string, message: string, serverInfo: string) => {
      const messageText = `[${serverInfo}] ${user}: ${message}`;
      this.messages = [...this.messages, messageText]; // Create new array reference
      this.cdr.detectChanges(); // Force change detection
    });

    // Start connection
    this.hubConnection.start()
      .then(() => {
        this.isConnected = true;
        console.log(`Connected to SignalR hub at ${this.serverUrl}`);
        this.cdr.detectChanges();
      })
      .catch(err => {
        console.error('Error connecting to SignalR hub:', err);
        this.isConnected = false;
        this.cdr.detectChanges();
      });
  }

  /**
   * Send message to current server
   * The backplane ensures ALL clients receive this message,
   * regardless of which server they're connected to
   */
  sendMessage() {
    if (this.hubConnection && this.isConnected && this.user && this.message) {
      // Send to current server - backplane will distribute to all servers
      this.hubConnection.invoke('SendMessage', this.user, this.message)
        .catch(err => {
          console.error('Error sending message:', err);
        });
      
      this.message = '';
    }
  }

  /**
   * Switch to a different server (simulates load balancing)
   * This demonstrates that messages flow through the backplane
   * regardless of which server you connect to
   */
  switchServer(url: string) {
    this.serverUrl = url;
    
    // Disconnect from current server
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
    
    // Connect to new server
    this.connectToSignalR();
  }
}
