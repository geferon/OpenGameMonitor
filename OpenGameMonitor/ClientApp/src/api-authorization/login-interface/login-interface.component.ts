import { Component, OnInit } from '@angular/core';
import { AuthorizeService } from '../authorize.service';
import { first } from 'rxjs/operators';

@Component({
	selector: 'app-login-interface',
	templateUrl: './login-interface.component.html',
	styleUrls: ['./login-interface.component.scss']
})
export class LoginInterfaceComponent implements OnInit {

	constructor(
		private authorizeService: AuthorizeService
	) { }

	ngOnInit(): void {
		this.authorizeService.getUser()
		.pipe(first())
		.subscribe((value) => {
			console.log(value);
			console.log(this.authorizeService.redirectUri());
		});
	}

}
