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
  private hubConnection: signalR.HubConnection | null = null;
  
  user = '';
  message = '';
  messages: string[] = [];
  isConnected = false;
  serverUrl = 'http://localhost:5001'; // Default to first replica

  constructor(private cdr: ChangeDetectorRef) {}

  ngOnInit() {
    this.connectToSignalR();
  }

  ngOnDestroy() {
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
  }

  connectToSignalR() {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`${this.serverUrl}/chatHub`)
      .build();

    this.hubConnection.on('ReceiveMessage', (user: string, message: string, serverInfo: string) => {
      const messageText = `[${serverInfo}] ${user}: ${message}`;
      this.messages.push(messageText);
      // Force change detection to update the UI immediately
      this.cdr.detectChanges();
    });

    this.hubConnection.start()
      .then(() => {
        this.isConnected = true;
        console.log('Connected to SignalR hub via WebSocket');
      })
      .catch(err => {
        console.error('Error connecting to SignalR hub:', err);
        this.isConnected = false;
      });
  }

  sendMessage() {
    if (this.hubConnection && this.isConnected && this.user && this.message) {
      // Don't add message locally - let the server broadcast it back
      this.hubConnection.invoke('SendMessage', this.user, this.message)
        .catch(err => {
          console.error('Error sending message:', err);
        });
      
      this.message = '';
    }
  }

  switchServer(url: string) {
    this.serverUrl = url;
    // Keep message history when switching servers
    if (this.hubConnection) {
      this.hubConnection.stop();
    }
    this.connectToSignalR();
  }
}
