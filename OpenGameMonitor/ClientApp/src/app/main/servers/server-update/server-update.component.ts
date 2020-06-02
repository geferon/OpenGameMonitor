import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
	selector: 'app-server-update',
	templateUrl: './server-update.component.html',
	styleUrls: ['./server-update.component.scss']
})
export class ServerUpdateComponent implements OnInit {

	constructor(
		private route: ActivatedRoute
	) { }

	public Id: number;

	ngOnInit(): void {
		this.Id = +this.route.snapshot.paramMap.get('id');
	}

}
