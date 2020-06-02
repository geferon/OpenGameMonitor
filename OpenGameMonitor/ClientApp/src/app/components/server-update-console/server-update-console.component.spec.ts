import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ServerUpdateConsoleComponent } from './server-update-console.component';

describe('ServerUpdateConsoleComponent', () => {
  let component: ServerUpdateConsoleComponent;
  let fixture: ComponentFixture<ServerUpdateConsoleComponent>;

  beforeEach(async(() => {
	TestBed.configureTestingModule({
		declarations: [ ServerUpdateConsoleComponent ]
	})
	.compileComponents();
  }));

  beforeEach(() => {
	fixture = TestBed.createComponent(ServerUpdateConsoleComponent);
	component = fixture.componentInstance;
	fixture.detectChanges();
  });

  it('should create', () => {
	expect(component).toBeTruthy();
  });
});
