import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../services/api.service';
import { UsuarioInfo } from '../../../services/auth.service';

@Component({
  selector: 'app-admin-usuarios',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <div class="page-header flex justify-between items-center">
        <div>
          <h2>⚙️ Gestão de Usuários & Contas Fictícias</h2>
          <p class="text-muted">Administração de usuários, papéis e vínculos com as instituições.</p>
        </div>
        <button class="btn btn-secondary btn-sm" (click)="carregarUsuarios()">🔄 Atualizar</button>
      </div>

      <div *ngIf="loading" class="text-muted">Carregando usuários...</div>

      <div class="card table-card" *ngIf="!loading">
        <table class="data-table">
          <thead>
            <tr>
              <th>Nome</th>
              <th>E-mail</th>
              <th>Perfil</th>
              <th>Instituição Vinculada</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let u of usuarios">
              <td><strong>{{ u.nome }}</strong></td>
              <td>{{ u.email }}</td>
              <td><span class="badge" [ngClass]="getRoleBadge(u.perfil)">{{ u.perfil }}</span></td>
              <td>{{ u.instituicaoNome || '— (Geral)' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 1.5rem; }
    .table-card { padding: 0; overflow: hidden; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 0.875rem; }
    .data-table th, .data-table td { padding: 0.875rem 1.25rem; border-bottom: 1px solid var(--border); }
    .data-table th { background: #f8fafc; font-weight: 600; color: #475569; }
  `]
})
export class AdminUsuariosComponent implements OnInit {
  private api = inject(ApiService);

  usuarios: UsuarioInfo[] = [];
  loading = false;

  ngOnInit() {
    this.carregarUsuarios();
  }

  carregarUsuarios() {
    this.loading = true;
    this.api.getUsuariosAdmin().subscribe({
      next: (res) => {
        this.usuarios = res;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  getRoleBadge(perfil: string): string {
    switch (perfil) {
      case 'Paciente': return 'badge-primary';
      case 'Medico': return 'badge-success';
      case 'Enfermeiro': return 'badge-info';
      case 'Atendente': return 'badge-warning';
      case 'Admin': return 'badge-danger';
      default: return 'badge-secondary';
    }
  }
}
