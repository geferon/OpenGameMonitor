import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { APIPaths } from './services.constants';
import { MonitorUser, Group } from '../definitions/interfaces';

@Injectable({
	providedIn: 'root'
})
export class UserService {
	constructor(
		private http: HttpClient
	) { }

	private httpOptions = {
		headers: new HttpHeaders({ 'Content-Type': 'application/json' })
	};

	// Fetch
	getUsers(): Observable<MonitorUser[]> {
		return this.http.get<MonitorUser[]>(APIPaths.Users);
	}

	getUser(id: number): Observable<MonitorUser> {
		return this.http.get<MonitorUser>(`${APIPaths.Users}/${id}`);
	}

	// Insert
	addUser(user: MonitorUser): Observable<MonitorUser> {
		return this.http.post<MonitorUser>(APIPaths.Users, user, this.httpOptions);
	}

	// Update
	updateUser(user: MonitorUser): Observable<any> {
		return this.http.patch(`${APIPaths.Users}/${user.UserName}`, user, this.httpOptions);
	}

	// Delete
	deleteUser(user: MonitorUser | number): Observable<MonitorUser> {
		const id = typeof user === 'number' ? user : user.UserName;

		return this.http.delete<MonitorUser>(`${APIPaths.Users}/${id}`, this.httpOptions);
	}


	// ---------
	// Groups

	getGroups(): Observable<Group[]> {
		return this.http.get<Group[]>(APIPaths.Groups, this.httpOptions);
	}
}
