import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Setting } from '../definitions/interfaces';
import { APIPaths } from './services.constants';

@Injectable({
	providedIn: 'root'
})
export class SettingsService {

	constructor(
		private http: HttpClient
	) { }

	private httpOptions = {
		headers: new HttpHeaders({ 'Content-Type': 'application/json' })
	};


	// Fetch
	getSettings(): Observable<Setting[]> {
		return this.http.get<Setting[]>(APIPaths.Settings);
	}

	// Update
	updateSetting(setting: Setting): Observable<any> {
		return this.http.patch(`${APIPaths.Settings}/${setting.Key}`, setting, this.httpOptions);
	}


}
