import type { Nivel } from '../api/client'

/**
 * Espelho das regras do backend (ChatApi/Autorizacao/Permissoes.cs). Aqui elas
 * servem so para esconder botao que o servidor recusaria — quem decide de
 * verdade e a API; o front nunca e a tranca.
 */
export function podeAdministrar(nivel: Nivel | null | undefined) {
  return nivel === 'Diretor' || nivel === 'Gerente'
}

export function podeGerenciarMembros(
  nivel: Nivel | null | undefined,
  usuarioId: string | undefined,
  supervisorDaEquipe: string | null | undefined,
) {
  return podeAdministrar(nivel) || (nivel === 'Supervisor' && supervisorDaEquipe === usuarioId)
}
