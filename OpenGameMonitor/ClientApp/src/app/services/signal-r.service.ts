import { Injectable, isDevMode } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject, Observable } from 'rxjs';
import { AuthorizeService } from '../../api-authorization/authorize.service';

@Injectable({
	providedIn: 'root'
})
export class SignalRService {

	constructor(
		private authorize: AuthorizeService
	) {
		this.initConnection();
	}

	private hubConnection: signalR.HubConnection;
	private cachedListeners: { [key: string]: Subject<any[]> } = {};

	private async initConnection(): Promise<void> {
		console.log("Initiating SignalR connection!");
		this.hubConnection = new signalR.HubConnectionBuilder()
			.withUrl('/hubs/servers', {accessTokenFactory: () => this.authorize.getAccessToken().toPromise()})
			.withAutomaticReconnect()
			.configureLogging(isDevMode() ? signalR.LogLevel.Debug : signalR.LogLevel.Warning)
			.build();

		try {
			await this.hubConnection.start();

			console.log("Connected to SignalR succesfully!");
		} catch (err) {
			console.error("There has been an error while trying to initiate the SignalR connection!", err);
		}
	}

	public listenToEvent(event: string): Observable<any[]> {
		if (this.cachedListeners[event]) {
			return this.cachedListeners[event].asObservable();
		}

		this.cachedListeners[event] = new Subject<any[]>();
		this.hubConnection.on(event, (...args) => {
			this.cachedListeners[event].next(args);
		});

		return this.cachedListeners[event].asObservable();
	}
}
