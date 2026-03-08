const trackingStates = new WeakMap();

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
}

function getGsap() {
    if (!window.gsap) {
        throw new Error("GSAP is not loaded. Ensure lib/gsap/gsap.min.js is loaded before Login.razor.js import.");
    }
    return window.gsap;
}

// Per-eye polar coordinate tracking: exactly matches animated-characters.tsx EyeBall logic.
// Each pupil/dot gets its own atan2 calculation from its own screen-space center.
function updatePupils(heroElement, state) {
    const pupils = heroElement.querySelectorAll(".pupil, .dot");
    const mouseX = state.mouseX;
    const mouseY = state.mouseY;
    const MAX_DISTANCE = 5;

    pupils.forEach((pupil) => {
        const parent = pupil.parentElement; // .eye or .eyes (for .dot)
        if (!parent) return;

        const rect = parent.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        let targetX, targetY;

        if (state.isShowPasswordState) {
            // Purple hides: pupils look far left (peeking upward-left)
            const char = pupil.closest(".character");
            if (char && char.classList.contains("purple")) {
                targetX = -MAX_DISTANCE;
                targetY = -MAX_DISTANCE;
            } else {
                // Others look up-right watching
                targetX = MAX_DISTANCE * 0.6;
                targetY = -MAX_DISTANCE * 0.4;
            }
        } else if (state.isLookingAtEachOther) {
            // Characters look at each other: use fixed mutual gaze angles
            const char = pupil.closest(".character");
            if (char) {
                if (char.classList.contains("purple")) {
                    targetX = MAX_DISTANCE;
                    targetY = 0;
                } else if (char.classList.contains("charcoal")) {
                    targetX = -MAX_DISTANCE;
                    targetY = 0;
                } else {
                    // orange & yellow look toward center
                    const deltaX = mouseX - centerX;
                    const deltaY = mouseY - centerY;
                    const dist = Math.min(Math.sqrt(deltaX * deltaX + deltaY * deltaY), MAX_DISTANCE);
                    const angle = Math.atan2(deltaY, deltaX);
                    targetX = Math.cos(angle) * dist;
                    targetY = Math.sin(angle) * dist;
                }
            } else {
                targetX = 0;
                targetY = 0;
            }
        } else {
            // Normal mouse tracking: per-eye polar coordinate calculation
            const deltaX = mouseX - centerX;
            const deltaY = mouseY - centerY;
            const dist = Math.min(Math.sqrt(deltaX * deltaX + deltaY * deltaY), MAX_DISTANCE);
            const angle = Math.atan2(deltaY, deltaX);
            targetX = Math.cos(angle) * dist;
            targetY = Math.sin(angle) * dist;
        }

        // Direct style assignment each frame (no CSS transition on pupil/dot — GSAP ticker drives it)
        pupil.style.transform = `translate(${targetX}px, ${targetY}px)`;
    });
}

function calculatePosition(element, mouseX, mouseY) {
    const rect = element.getBoundingClientRect();
    const centerX = rect.left + rect.width / 2;
    const centerY = rect.top + rect.height / 3;

    const deltaX = mouseX - centerX;
    const deltaY = mouseY - centerY;

    return {
        faceX: clamp(deltaX / 20, -15, 15),
        faceY: clamp(deltaY / 30, -10, 10),
        bodySkew: clamp(-deltaX / 120, -6, 6),
    };
}

function setCharacterMotion(heroElement, state) {
    const purple = heroElement.querySelector(".character.purple");
    const charcoal = heroElement.querySelector(".character.charcoal");
    const orange = heroElement.querySelector(".character.orange");
    const yellow = heroElement.querySelector(".character.yellow");
    const purpleEyes = heroElement.querySelector(".character.purple .eyes");
    const charcoalEyes = heroElement.querySelector(".character.charcoal .eyes");
    const orangeEyes = heroElement.querySelector(".character.orange .eyes");
    const yellowEyes = heroElement.querySelector(".character.yellow .eyes");
    const mouth = heroElement.querySelector(".character.yellow .mouth");

    if (!purple || !charcoal || !orange || !yellow ||
        !purpleEyes || !charcoalEyes || !orangeEyes || !yellowEyes || !mouth) {
        return;
    }

    const purplePos = calculatePosition(purple, state.mouseX, state.mouseY);
    const charcoalPos = calculatePosition(charcoal, state.mouseX, state.mouseY);
    const orangePos = calculatePosition(orange, state.mouseX, state.mouseY);
    const yellowPos = calculatePosition(yellow, state.mouseX, state.mouseY);

    // Body skew (CSS transition: 0.7s ease-in-out on .character)
    purple.style.height = (state.isTyping || state.isHidingPassword) ? "440px" : "400px";
    if (state.isShowPasswordState) {
        purple.style.transform = "skewX(0deg)";
    } else if (state.isTyping || state.isHidingPassword) {
        purple.style.transform = `skewX(${purplePos.bodySkew - 12}deg) translateX(40px)`;
    } else {
        purple.style.transform = `skewX(${purplePos.bodySkew}deg)`;
    }

    if (state.isShowPasswordState) {
        charcoal.style.transform = "skewX(0deg)";
    } else if (state.isTyping) {
        charcoal.style.transform = `skewX(${(charcoalPos.bodySkew * 1.5) + 10}deg) translateX(20px)`;
    } else if (state.isHidingPassword) {
        charcoal.style.transform = `skewX(${charcoalPos.bodySkew * 1.5}deg)`;
    } else {
        charcoal.style.transform = `skewX(${charcoalPos.bodySkew}deg)`;
    }

    orange.style.transform = state.isShowPasswordState ? "skewX(0deg)" : `skewX(${orangePos.bodySkew}deg)`;
    yellow.style.transform = state.isShowPasswordState ? "skewX(0deg)" : `skewX(${yellowPos.bodySkew}deg)`;

    // Eyes container position (CSS transition: 0.2s ease-out on .eyes)
    if (state.isShowPasswordState) {
        purpleEyes.style.left = "20px";
        purpleEyes.style.top = "35px";
        charcoalEyes.style.left = "10px";
        charcoalEyes.style.top = "28px";
        orangeEyes.style.left = "50px";
        orangeEyes.style.top = "85px";
        yellowEyes.style.left = "20px";
        yellowEyes.style.top = "35px";
        mouth.style.left = "10px";
        mouth.style.top = "88px";
        mouth.style.height = "4px";
        mouth.style.borderRadius = "999px";
    } else {
        purpleEyes.style.left = `${state.isLookingAtEachOther ? 55 : (45 + purplePos.faceX)}px`;
        purpleEyes.style.top = `${state.isLookingAtEachOther ? 65 : (40 + purplePos.faceY)}px`;
        charcoalEyes.style.left = `${state.isLookingAtEachOther ? 32 : (26 + charcoalPos.faceX)}px`;
        charcoalEyes.style.top = `${state.isLookingAtEachOther ? 12 : (32 + charcoalPos.faceY)}px`;
        orangeEyes.style.left = `${82 + orangePos.faceX}px`;
        orangeEyes.style.top = `${90 + orangePos.faceY}px`;
        yellowEyes.style.left = `${52 + yellowPos.faceX}px`;
        yellowEyes.style.top = `${40 + yellowPos.faceY}px`;
        mouth.style.left = `${40 + yellowPos.faceX}px`;
        mouth.style.top = `${88 + yellowPos.faceY}px`;

        if (state.isTyping) {
            mouth.style.height = "18px";
            mouth.style.borderRadius = "30px";
        } else {
            mouth.style.height = "4px";
            mouth.style.borderRadius = "999px";
        }
    }
}

function deriveInputState(state) {
    const usernameInput = state.usernameInput;
    const passwordInput = state.passwordInput;

    const isTyping = !!(usernameInput && document.activeElement === usernameInput);
    const passwordLength = passwordInput ? passwordInput.value.length : 0;
    const showPassword = !!(passwordInput && passwordInput.type === "text");
    const isHidingPassword = passwordLength > 0 && !showPassword;
    const isShowPasswordState = passwordLength > 0 && showPassword;

    state.isTyping = isTyping;
    state.isHidingPassword = isHidingPassword;
    state.isShowPasswordState = isShowPasswordState;

    // isLookingAtEachOther: set on username focus, cleared after 800ms
    // managed via focus/blur handlers — only reset here if typing stopped
    if (!isTyping && state._lookingTimerPending) {
        // still counting down — leave isLookingAtEachOther as-is
    }
}

function scheduleRandomBlink(heroElement, selector, state) {
    const gsap = getGsap();

    // Pre-initialize inline height so GSAP can tween from a known value
    const eyes = heroElement.querySelectorAll(selector);
    eyes.forEach((eye) => {
        const normalHeight = parseFloat(getComputedStyle(eye).height);
        eye.dataset.normalHeight = normalHeight;
        gsap.set(eye, { height: normalHeight });
    });

    function blink() {
        if (!trackingStates.has(heroElement)) return; // disposed

        const currentEyes = heroElement.querySelectorAll(selector);
        if (currentEyes.length === 0) return;

        // Blink all eyes of this character simultaneously
        currentEyes.forEach((eye) => {
            const normalHeight = parseFloat(eye.dataset.normalHeight) || 18;
            gsap.timeline()
                .to(eye, { height: 2, duration: 0.075, ease: "power2.in" })
                .to(eye, { height: normalHeight, duration: 0.075, ease: "power2.out" });
        });

        // Schedule next blink: random 3000–7000ms (matches original)
        const nextDelay = Math.random() * 4000 + 3000;
        const timer = window.setTimeout(blink, nextDelay);
        state.blinkTimers.push(timer);
    }

    // Start after a random initial delay (stagger the two characters)
    const initialDelay = Math.random() * 3000 + 1000;
    const timer = window.setTimeout(blink, initialDelay);
    state.blinkTimers.push(timer);
}

function cleanupHeroTracking(heroElement, state) {
    if (!state) {
        return;
    }

    if (state.observer) {
        state.observer.disconnect();
        state.observer = null;
    }

    // Remove GSAP ticker
    if (state.tickerFn) {
        getGsap().ticker.remove(state.tickerFn);
        state.tickerFn = null;
    }

    // Remove mousemove
    if (state.moveHandler) {
        window.removeEventListener("mousemove", state.moveHandler);
        state.moveHandler = null;
    }

    // Remove all input handlers
    state.inputHandlers.forEach(({ el, type, fn }) => {
        el.removeEventListener(type, fn);
    });
    state.inputHandlers = [];

    // Clear all blink timers
    state.blinkTimers.forEach((t) => window.clearTimeout(t));
    state.blinkTimers = [];

    // Clear looking timer
    if (state._lookingTimer) {
        window.clearTimeout(state._lookingTimer);
        state._lookingTimer = null;
    }

    trackingStates.delete(heroElement);
}

function createCleanupObserver(heroElement, state) {
    if (!document.body) {
        return;
    }

    const observer = new MutationObserver(() => {
        if (!document.body.contains(heroElement)) {
            cleanupHeroTracking(heroElement, state);
        }
    });

    observer.observe(document.body, { childList: true, subtree: true });
    state.observer = observer;
}

export function initHeroTracking(heroElement) {
    if (!heroElement || trackingStates.has(heroElement)) {
        return;
    }

    const gsap = getGsap();

    const usernameInput = document.getElementById("username");
    const passwordInput = document.getElementById("password");
    const passwordToggle = document.querySelector(".password-toggle");

    const state = {
        mouseX: window.innerWidth / 2,
        mouseY: window.innerHeight / 2,
        isTyping: false,
        isHidingPassword: false,
        isShowPasswordState: false,
        isLookingAtEachOther: false,
        _lookingTimerPending: false,
        _lookingTimer: null,
        blinkTimers: [],
        moveHandler: null,
        inputHandlers: [],
        tickerFn: null,
        usernameInput: (usernameInput instanceof HTMLInputElement) ? usernameInput : null,
        passwordInput: (passwordInput instanceof HTMLInputElement) ? passwordInput : null,
        passwordToggle: (passwordToggle instanceof HTMLElement) ? passwordToggle : null,
        observer: null,
    };

    createCleanupObserver(heroElement, state);

    // GSAP ticker: runs every frame, drives pupil tracking + body/eye updates
    state.tickerFn = () => {
        deriveInputState(state);
        setCharacterMotion(heroElement, state);
        updatePupils(heroElement, state);
    };
    gsap.ticker.add(state.tickerFn);

    // mousemove: only update coordinates, no DOM ops
    state.moveHandler = (event) => {
        state.mouseX = event.clientX;
        state.mouseY = event.clientY;
    };
    window.addEventListener("mousemove", state.moveHandler, { passive: true });

    // Username focus → trigger isLookingAtEachOther for 800ms
    if (state.usernameInput) {
        const onUsernameFocus = () => {
            if (state._lookingTimer) {
                window.clearTimeout(state._lookingTimer);
            }
            state.isLookingAtEachOther = true;
            state._lookingTimerPending = true;
            state._lookingTimer = window.setTimeout(() => {
                state.isLookingAtEachOther = false;
                state._lookingTimerPending = false;
                state._lookingTimer = null;
            }, 800);
        };
        state.usernameInput.addEventListener("focus", onUsernameFocus);
        state.usernameInput.addEventListener("input", onUsernameFocus);
        state.inputHandlers.push({ el: state.usernameInput, type: "focus", fn: onUsernameFocus });
        state.inputHandlers.push({ el: state.usernameInput, type: "input", fn: onUsernameFocus });
    }

    // Random blinking for purple and charcoal (matches original: only these two characters)
    scheduleRandomBlink(heroElement, ".character.purple .eye", state);
    scheduleRandomBlink(heroElement, ".character.charcoal .eye", state);

    // Run once immediately to set initial state
    deriveInputState(state);
    setCharacterMotion(heroElement, state);
    updatePupils(heroElement, state);

    trackingStates.set(heroElement, state);
}
