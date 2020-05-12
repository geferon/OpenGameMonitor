import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { LoginInterfaceComponent } from './login-interface.component';

describe('LoginInterfaceComponent', () => {
  let component: LoginInterfaceComponent;
  let fixture: ComponentFixture<LoginInterfaceComponent>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ LoginInterfaceComponent ]
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(LoginInterfaceComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
