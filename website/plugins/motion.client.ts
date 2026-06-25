type ParallaxValue =
  | number
  | false
  | {
      tilt?: number
      shift?: number
      scale?: number
      perspective?: number
    }

type MotionElement = HTMLElement & {
  __parallaxCleanup?: () => void
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value))
}

export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.directive('parallax', {
    mounted(el: MotionElement, binding: { value?: ParallaxValue }) {
      if (binding.value === false) {
        return
      }

      const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      const supportsHover = window.matchMedia('(hover: hover) and (pointer: fine)').matches
      if (prefersReducedMotion || !supportsHover) {
        return
      }

      const rawValue = binding.value
      const options =
        typeof rawValue === 'number'
          ? { tilt: rawValue }
          : rawValue ?? {}

      const tilt = options.tilt ?? 10
      const shift = options.shift ?? 10
      const scale = options.scale ?? 1
      const perspective = options.perspective ?? 960

      el.classList.add('parallax-panel', 'parallax-glow')
      el.style.setProperty('--parallax-perspective', `${perspective}px`)
      el.style.setProperty('--parallax-scale', String(scale))

      let frame = 0
      let pointerX = 0
      let pointerY = 0

      const render = () => {
        frame = 0
        el.style.setProperty('--parallax-rx', String(pointerX * tilt))
        el.style.setProperty('--parallax-ry', String(pointerY * tilt))
        el.style.setProperty('--parallax-tx', String(pointerX * shift))
        el.style.setProperty('--parallax-ty', String(pointerY * shift))
        el.style.setProperty('--parallax-glow-x', `${((pointerX + 1) / 2) * 100}%`)
        el.style.setProperty('--parallax-glow-y', `${((pointerY + 1) / 2) * 100}%`)
      }

      const queue = () => {
        if (!frame) {
          frame = window.requestAnimationFrame(render)
        }
      }

      const onMove = (event: PointerEvent) => {
        const rect = el.getBoundingClientRect()
        const x = (event.clientX - rect.left) / rect.width
        const y = (event.clientY - rect.top) / rect.height

        pointerX = clamp((x - 0.5) * 2, -1, 1)
        pointerY = clamp((y - 0.5) * 2, -1, 1)
        queue()
      }

      const reset = () => {
        pointerX = 0
        pointerY = 0
        queue()
      }

      el.addEventListener('pointermove', onMove)
      el.addEventListener('pointerleave', reset)

      el.__parallaxCleanup = () => {
        if (frame) {
          window.cancelAnimationFrame(frame)
        }

        el.removeEventListener('pointermove', onMove)
        el.removeEventListener('pointerleave', reset)
      }
    },

    unmounted(el: MotionElement) {
      el.__parallaxCleanup?.()
      delete el.__parallaxCleanup
    },
  })
})
