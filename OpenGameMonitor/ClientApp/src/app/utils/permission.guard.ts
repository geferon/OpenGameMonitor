import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, RouterModule } from '@angular/router';
import { Observable, combineLatest, of } from 'rxjs';
import { AuthorizeService } from '../../api-authorization/authorize.service';
import { map, mergeMap } from 'rxjs/operators';

@Injectable({
	providedIn: 'root'
})
export class PermissionGuard implements CanActivate {
	constructor(private authorize: AuthorizeService) { }

	canActivate(
		next: ActivatedRouteSnapshot,
		state: RouterStateSnapshot): Observable<boolean> | Promise<boolean> | boolean {
		return this.authorize.isAuthenticated()
			.pipe(mergeMap(result => {
				if (!result) {
					return of(result);
				}

				return combineLatest((next.data.permissions as string[])
					.map(perm => this.authorize.hasUserPermission(perm)))
					.pipe(map(bools => bools.reduce((a, b) => a && b)));
			}));
	}

}
