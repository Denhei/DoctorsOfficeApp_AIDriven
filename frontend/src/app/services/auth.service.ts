import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_URL } from './api-url';

export interface UsuarioInfo {
  id: string;
  nome: string;
  email: string;
  perfil: 'Paciente' | 'Atendente' | 'Enfermeiro' | 'Medico' | 'Admin';
  instituicaoId?: string;
  instituicaoNome?: string;
  pacienteId?: string;
  medicoId?: string;
}

export interface AuthResponse {
  token: string;
  expiraEmUtc: string;
  usuario: UsuarioInfo;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly apiUrl = API_URL;
  private readonly tokenKey = 'consultorio_token';
  private readonly userKey = 'consultorio_user';

  currentUser = signal<UsuarioInfo | null>(this.getStoredUser());

  constructor(private http: HttpClient, private router: Router) {}

  login(email: string, senha: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.apiUrl}/auth/login`, { email, senha }).pipe(
      tap(response => {
        localStorage.setItem(this.tokenKey, response.token);
        localStorage.setItem(this.userKey, JSON.stringify(response.usuario));
        this.currentUser.set(response.usuario);
      })
    );
  }

  logout(): void {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.userKey);
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  isAuthenticated(): boolean {
    return !!this.getToken() && !!this.currentUser();
  }

  private getStoredUser(): UsuarioInfo | null {
    const raw = localStorage.getItem(this.userKey);
    if (!raw) return null;
    try {
      return JSON.parse(raw);
    } catch {
      return null;
    }
  }
}
