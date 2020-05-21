import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { ServerUpdateComponent } from './server-update.component';

describe('ServerUpdateComponent', () => {
  let component: ServerUpdateComponent;
  let fixture: ComponentFixture<ServerUpdateComponent>;

  beforeEach(async(() => {
	TestBed.configureTestingModule({
		declarations: [ ServerUpdateComponent ]
	})
	.compileComponents();
  }));

  beforeEach(() => {
	fixture = TestBed.createComponent(ServerUpdateComponent);
	component = fixture.componentInstance;
	fixture.detectChanges();
  });

  it('should create', () => {
	expect(component).toBeTruthy();
  });
});
