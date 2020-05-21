import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, Subject, BehaviorSubject } from 'rxjs';
import { Server, Game } from '../definitions/interfaces';
import { APIPaths } from './services.constants';
import { SignalRService } from './signal-r.service';
import { share, finalize, map } from 'rxjs/operators';

@Injectable({
	providedIn: 'root'
})
export class ServerService {
	constructor(
		private http: HttpClient,
		private signalR: SignalRService
	) { }

	private httpOptions = {
		headers: new HttpHeaders({ 'Content-Type': 'application/json' })
	};

	// Fetch
	getServers(): Observable<Server[]> {
		return this.http.get<Server[]>(APIPaths.Servers);
	}

	getServersRealtime(): Observable<Server[]> {
		let subject = new Subject<Server[]>();

		let currentServers = [];

		this.getServers().subscribe((servers) => {
			subject.next(servers);
			currentServers = servers;
		});

		let subscriptions = [
			this.signalR.listenToEvent("Server:Updated")
				.subscribe(([server]: [Server]) => {
					currentServers[currentServers.findIndex(serv => serv.Id = server.Id)] = server;

					console.log("WS - Server updated!", server);

					subject.next(currentServers);
				}),
			this.signalR.listenToEvent("Server:Inserted")
				.subscribe(([server]: [Server]) => {
					currentServers.push(server);

					console.log("WS - Server inserted!", server);

					subject.next(currentServers);
				}),
			this.signalR.listenToEvent("Server:Deleted")
				.subscribe(([serverId]: [number]) => {
					let index = currentServers.findIndex(serv => serv.Id == serverId);

					console.log("WS - Server deleted!", serverId, index);

					if (index >= 0) {
						currentServers.splice(index, 1);

						subject.next(currentServers);
					}
				})
		];

		subject.pipe(
			finalize(() => {
				for (let sub of subscriptions) {
					sub.unsubscribe();
				}
			}),
			share()
		);

		return subject.asObservable();
	}

	// Sub parts
	unsubscribeFromServers(): Observable<any> {
		return this.signalR.sendEvent("StopListenServerConsole");
	}

	subscribeToServer(server: Server | number): Observable<any> {
		const id = typeof server === 'number' ? server : server.Id;
		return this.signalR.sendEvent("ListenServerConsole", id);
	}

	// Gets
	getServerUpdated(): Observable<Server> {
		return this.signalR.listenToEvent("Server:Updated").pipe(map(([server]: [Server]) => server));
	}
	getServerInserted(): Observable<Server> {
		return this.signalR.listenToEvent("Server:Inserted").pipe(map(([server]: [Server]) => server));
	}
	getServerDeleted(): Observable<number> {
		return this.signalR.listenToEvent("Server:Deleted").pipe(map(([serverId]: [number]) => serverId));
	}

	getServersConsoleMessages(): Observable<any> {
		return this.signalR.listenToEvent("Server:ConsoleMessage");
	}

	getServersUpdateMessages(): Observable<any> {
		return this.signalR.listenToEvent("Server:UpdateMessage");
	}

	getServersUpdateProgress(): Observable<any> {
		return this.signalR.listenToEvent("Server:UpdateProgress");
	}

	getServer(id: number): Observable<Server> {
		return this.http.get<Server>(`${APIPaths.Servers}/${id}`);
	}

	// Insert
	addServer(server: Server): Observable<Server> {
		return this.http.post<Server>(APIPaths.Servers, server, this.httpOptions);
	}

	// Update
	updateServer(server: Server): Observable<any> {
		return this.http.patch(`${APIPaths.Servers}/${server.Id}`, server, this.httpOptions);
	}

	// Delete
	deleteServer(server: Server | number): Observable<Server> {
		const id = typeof server === 'number' ? server : server.Id;

		return this.http.delete<Server>(`${APIPaths.Servers}/${id}`, this.httpOptions);
	}

	// Do action
	startServer(server: Server | number): Observable<any> {
		const id = typeof server === 'number' ? server : server.Id;

		return this.http.post<boolean>(`${APIPaths.Servers}/${id}/start`, this.httpOptions);
	}

	stopServer(server: Server | number): Observable<any> {
		const id = typeof server === 'number' ? server : server.Id;

		return this.http.post<boolean>(`${APIPaths.Servers}/${id}/stop`, this.httpOptions);
	}

	startServerUpdate(server: Server | number): Observable<any> {
		const id = typeof server === 'number' ? server : server.Id;

		return this.http.post<boolean>(`${APIPaths.Servers}/${id}/update`, this.httpOptions);
	}


	// -----------
	// Games

	// Fetch
	getGames(): Observable<Game[]> {
		return this.http.get<Game[]>(APIPaths.Games);
	}

	addGame(game: Game): Observable<Game> {
		return this.http.post<Game>(APIPaths.Games, game, this.httpOptions);
	}
}
