import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject, pipe } from 'rxjs';
import { takeUntil, filter } from 'rxjs/operators';
import { ServerService } from '../../../services/server.service';

@Component({
	selector: 'app-server-console',
	templateUrl: './server-console.component.html',
	styleUrls: ['./server-console.component.scss']
})
export class ServerConsoleComponent implements OnInit, OnDestroy {

	constructor(
		private route: ActivatedRoute,
		private servers: ServerService
	) { }

	public Id: number;
	public Lines: string[] = [];

	private readonly onDestroy = new Subject();

	ngOnInit(): void {
		this.Id = +this.route.snapshot.paramMap.get('id');

		this.servers.subscribeToServer(this.Id);

		this.servers.getServersConsoleMessages()
			.pipe(
				takeUntil(this.onDestroy),
				filter(([serverId, line]: [number, string]) => {
					return serverId == this.Id;
				})
			)
			.subscribe(([serverId, line]: [number, string]) => {
				this.Lines.push(line);
			});
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();

		this.servers.unsubscribeFromServers();
	}

}
