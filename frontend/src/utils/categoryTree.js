/**
 * 分类树工具函数。
 *
 * 后端 /api/categories 返回的是树形结构（CategoryDto 含 Children 字段）：
 *   { id, name, noteCount, createdAt, parentId, children: [...] }
 * 本模块提供树形分类的扁平化、查找、路径拼接等辅助能力，
 * 供侧栏导航、选择器、编辑页等组件复用。
 */

/**
 * 将树形分类扁平化为一维数组（深度优先），保留每个节点的层级信息。
 * @param {Array} tree 树形分类数组
 * @param {number} depth 当前深度（内部递归用）
 * @returns {Array<{...node, depth:number}>}
 */
export function flattenCategories(tree, depth = 0) {
  const result = []
  for (const node of tree || []) {
    result.push({ ...node, depth })
    if (node.children && node.children.length > 0) {
      result.push(...flattenCategories(node.children, depth + 1))
    }
  }
  return result
}

/**
 * 在树形分类中按 id 查找节点（深度优先）。
 * @param {Array} tree
 * @param {number|string} id
 * @returns {object|null}
 */
export function findCategoryById(tree, id) {
  if (id === null || id === undefined) return null
  for (const node of tree || []) {
    if (node.id === id) return node
    if (node.children && node.children.length > 0) {
      const found = findCategoryById(node.children, id)
      if (found) return found
    }
  }
  return null
}

/**
 * 获取某分类的完整路径（如「工作/项目A/周报」），用于展示。
 * @param {Array} tree 树形分类数组
 * @param {number|string} id
 * @returns {string} 用「/」拼接的路径；未找到返回空串
 */
export function getCategoryPath(tree, id) {
  const parts = []
  let currentId = id
  const visited = new Set()
  while (currentId !== null && currentId !== undefined && !visited.has(currentId)) {
    visited.add(currentId)
    const node = findCategoryById(tree, currentId)
    if (!node) break
    parts.unshift(node.name)
    currentId = node.parentId
  }
  return parts.join(' / ')
}

/**
 * 收集树中所有节点的 id（含子孙），用于「父分类包含子分类」等判断。
 * @param {object} node 单个分类节点
 * @returns {number[]}
 */
export function collectDescendantIds(node) {
  const ids = []
  const stack = [node]
  while (stack.length > 0) {
    const cur = stack.pop()
    if (!cur) continue
    ids.push(cur.id)
    if (cur.children && cur.children.length > 0) {
      for (const child of cur.children) stack.push(child)
    }
  }
  return ids
}
