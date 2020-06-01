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
export class ServerConsoleComponent implements OnInit {

	constructor(
		private route: ActivatedRoute
	) { }

	public Id: number;

	ngOnInit(): void {
		this.Id = +this.route.snapshot.paramMap.get('id');
	}

}
