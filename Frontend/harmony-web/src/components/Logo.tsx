/** The HARMONY lockup: the woven H and the wordmark, in currentColor, from the brand set (outputs/brand). */
export function Lockup({ height = 17 }: { height?: number }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 -3 359.333 46" role="img" aria-label="HARMONY" style={{ height, width: 'auto', display: 'block' }}>
      <g transform="translate(0 -3) scale(1.917)">
        <path fill="currentColor" d="M0 0 L5.5 0 L5.5 6 L0 6 Z M0 12 L5.5 12 L5.5 24 L0 24 Z M15.5 0 L21 0 L21 12 L15.5 12 Z M15.5 18 L21 18 L21 24 L15.5 24 Z M0 7 L14.5 7 L14.5 11 L0 11 Z M6.5 13 L21 13 L21 17 L6.5 17 Z" />
      </g>
      <path fill="currentColor" fillRule="evenodd" d="M61.333 0 L67.133 0 L67.133 40 L61.333 40 Z M85.533 0 L91.333 0 L91.333 40 L85.533 40 Z M67.133 17.4 L85.533 17.4 L85.533 22.6 L67.133 22.6 Z M100.333 40 L114.333 0 L121.333 0 L135.333 40 L128.976 40 L117.833 8.163 L106.69 40 Z M114.6 17.4 L121.066 17.4 L122.886 22.6 L112.78 22.6 Z M144.333 0 L150.133 0 L150.133 40 L144.333 40 Z M150.133 0 L159.933 0 A11.3 11.3 0 0 1 159.933 22.6 L150.133 22.6 L150.133 17.4 L159.533 17.4 A6.1 6.1 0 0 0 159.533 5.2 L150.133 5.2 Z M155.833 22.6 L162.765 22.6 L172.833 40 L165.901 40 Z M183.333 40 L183.333 0 L189.824 0 L202.833 31.538 L215.843 0 L222.333 0 L222.333 40 L216.533 40 L216.533 14.061 L205.833 40 L199.833 40 L189.133 14.061 L189.133 40 Z M232.833 20 A20.5 20.5 0 1 0 273.833 20 A20.5 20.5 0 1 0 232.833 20 Z M239.033 20 A14.3 15 0 1 1 267.633 20 A14.3 15 0 1 1 239.033 20 Z M284.333 40 L284.333 0 L291.331 0 L309.533 30.334 L309.533 0 L315.333 0 L315.333 40 L308.336 40 L290.133 9.666 L290.133 40 Z M324.333 0 L331.476 0 L341.833 16.032 L352.19 0 L359.333 0 L344.733 22.6 L344.733 40 L338.933 40 L338.933 22.6 Z" />
    </svg>
  )
}

/** The woven H alone, for tight places. */
export function Symbol({ size = 24 }: { size?: number }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 21 24" role="img" aria-label="HARMONY" style={{ height: size, width: 'auto', display: 'block' }}>
      <path fill="currentColor" fillRule="evenodd" d="M0 0 L5.5 0 L5.5 6 L0 6 Z M0 12 L5.5 12 L5.5 24 L0 24 Z M15.5 0 L21 0 L21 12 L15.5 12 Z M15.5 18 L21 18 L21 24 L15.5 24 Z M0 7 L14.5 7 L14.5 11 L0 11 Z M6.5 13 L21 13 L21 17 L6.5 17 Z" />
    </svg>
  )
}
