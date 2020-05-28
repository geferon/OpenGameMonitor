import { Component, OnInit, OnDestroy, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subject, pipe, from } from 'rxjs';
import { takeUntil, filter, mergeMap } from 'rxjs/operators';
import { ServerService } from '../../../services/server.service';
import { VIRTUAL_SCROLL_STRATEGY } from '@angular/cdk/scrolling';

@Component({
	selector: 'app-server-console',
	templateUrl: './server-console.component.html',
	styleUrls: ['./server-console.component.scss'],
	changeDetection: ChangeDetectionStrategy.OnPush,
	// providers: [{provide: VIRTUAL_SCROLL_STRATEGY, useClass:  }]
})
export class ServerConsoleComponent implements OnInit, OnDestroy {

	constructor(
		private route: ActivatedRoute,
		private servers: ServerService,
		private cdRef: ChangeDetectorRef
	) { }

	public Id: number;
	public Lines: string[] = [];

	private readonly onDestroy = new Subject();

	ngOnInit(): void {
		this.Id = +this.route.snapshot.paramMap.get('id');

		from(this.servers.subscribeToServer(this.Id))
			.pipe(
				mergeMap(() => this.servers.getServersConsoleMessages()),
				takeUntil(this.onDestroy),
				filter(([serverId, line]: [number, string]) => {
					return serverId == this.Id;
				})
			)
			.subscribe(([serverId, line]: [number, string]) => {
				// this.Lines.push(line);
				this.Lines = [...this.Lines, line];
				// console.log(this.Lines);
				this.cdRef.detectChanges();
			});
	}

	ngOnDestroy(): void {
		this.onDestroy.next();
		this.onDestroy.complete();

		let subs = this.servers.unsubscribeFromServers().subscribe(() => subs.unsubscribe());
	}

}
