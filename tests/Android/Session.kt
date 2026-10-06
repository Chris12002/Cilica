package dev.cilica

import java.util.concurrent.CompletableFuture
import java.util.concurrent.CountDownLatch
import java.util.concurrent.ExecutionException
import java.util.concurrent.TimeUnit
import dev.cilica.CilicaSessionRuntime as Runtime
import dev.cilica.CilicaSessionRuntime.Value as Value

private fun <T> CompletableFuture<T>.await(): T = get(15, TimeUnit.SECONDS)
private fun fails(reason: String, action: () -> CompletableFuture<*>) {
    try { action().await(); error("Expected $reason") }
    catch (error: ExecutionException) { check((error.cause as Runtime.Failure).reason == reason) { error.toString() } }
}
private fun Runtime.Outcome.handle() = (value as Value.Reference).handle!!
private fun Runtime.Outcome.fields() = (value as Value.Record).fields
private fun read(runtime: Runtime, session: Runtime.Handle) = runtime.invoke(3, listOf(Value.Reference(session))).await().fields()

object Session {
    @JvmStatic fun main(args: Array<String>) {
        var cycles = 0
        for (stress in listOf(false, true)) repeat(3) {
            val runtime = Runtime(stress)
            val other = Runtime(stress)
            try {
                runtime.ready().await(); other.ready().await()
                val units = charArrayOf('A', '\u0000', '\ud800')
                val label = runtime.importUTF16(units).await()
                check(runtime.copyUTF16(label).await().contentEquals(units))
                val empty = runtime.importUTF16(charArrayOf()).await()
                check(runtime.copyUTF16(empty).await().isEmpty()); runtime.release(empty).await()
                val session = runtime.invoke(1, listOf(Value.Reference(label))).await().handle()
                runtime.release(label).await()
                val alias = runtime.clone(session).await(); runtime.release(session).await()
                fails("invalidHandle") { runtime.clone(session) }
                fails("invalidHandle") { other.clone(alias) }
                val first = runtime.invoke(4, listOf(Value.Reference(alias))).await().handle()
                var text = String(units)
                var total = 9007199254740993L
                for (delta in 1..12) {
                    val fields = runtime.invoke(2, listOf(Value.Reference(alias), Value.Int32(delta))).await().fields()
                    total += delta; text += ":$delta"
                    check(fields == listOf(Value.Int32(delta), Value.Integer64(total), Value.Int32(text.length)))
                }
                val snapshot = runtime.invoke(4, listOf(Value.Reference(alias))).await().handle()
                check(String(runtime.copyUTF16(snapshot).await()) == text)
                check(runtime.copyUTF16(first).await().contentEquals(units))
                val before = read(runtime, alias)
                val fault = runtime.invoke(2, listOf(Value.Reference(alias), Value.Int32(-1)), true).await()
                check(fault.fault != null && fault.fault!!.imageHash.length == 64)
                val exception = fault.exception!!
                val detail = runtime.exceptionDetails(exception).await()
                check(detail.typeName == "System.InvalidOperationException" && detail.message == "negative delta") { detail.toString() }
                check(detail.typeToken != 0L && detail.imageHash.length == 64)
                check(read(runtime, alias) == before)
                val hold = runtime.pause().await(); val second = runtime.pause().await()
                check(runtime.lifecycle().await() == "paused")
                fails("paused") { runtime.invoke(3, listOf(Value.Reference(alias))) }
                fails("paused") { runtime.clone(alias) }
                fails("paused") { runtime.importUTF16(units) }
                check(runtime.exceptionDetails(exception).await() == detail)
                check(String(runtime.copyUTF16(snapshot).await()) == text)
                runtime.release(snapshot).await()
                fails("invalidPause") { other.resume(hold) }
                runtime.resume(hold).await()
                fails("invalidPause") { runtime.resume(hold) }
                fails("paused") { runtime.invoke(3, listOf(Value.Reference(alias))) }
                runtime.resume(second).await()
                val echo = runtime.invoke(5, listOf(Value.Record(1, listOf(Value.Int32(-7), Value.Integer64(Long.MIN_VALUE), Value.Int32(8))))).await().fields()
                check(echo == listOf(Value.Int32(-7), Value.Integer64(Long.MIN_VALUE), Value.Int32(8)))
                val callers = (1..16).map { CompletableFuture.supplyAsync {
                    runtime.invoke(2, listOf(Value.Reference(alias), Value.Int32(1))).await()
                } }
                callers.forEach { check(it.await().fault == null) }
                val final = read(runtime, alias)
                check(final[0] == Value.Int32(28) && final[1] == Value.Integer64(total+16))
                check(runtime.exceptionDetails(exception).await() == detail)
                val holds = (1..64).map { runtime.pause().await() }
                fails("pauseLimit") { runtime.pause() }
                holds.forEach { runtime.resume(it).await() }
                runtime.pause().await()
                runtime.release(exception).await(); runtime.release(first).await()
                runtime.close().await(); runtime.close().await()
                fails("closed") { runtime.clone(alias) }
                cycles++
            } finally { runtime.close().await(); other.close().await() }
        }
        // Deterministic backpressure/close test. Deliberately hold one completion on the worker.
        val runtime = Runtime()
        val entered = CountDownLatch(1); val release = CountDownLatch(1)
        try {
            val caller = Thread.currentThread()
            var first: CompletableFuture<Boolean>
            var attempts = 0
            do {
                check(attempts++ < 1000)
                first = runtime.ready().thenApply {
                    // A completed future invokes callbacks inline; never block the test caller.
                    if (Thread.currentThread() === caller) false
                    else { entered.countDown(); check(release.await(10, TimeUnit.SECONDS)); true }
                }
            } while (first.isDone && !first.await())
            check(entered.await(10, TimeUnit.SECONDS))
            val pending = (1..256).map { runtime.ready() }
            fails("queueLimit") { runtime.ready() }
            pending.first().cancel(true) // Cancelling observation does not remove admitted work.
            val close = runtime.close()
            fails("closed") { runtime.ready() }
            release.countDown(); first.await(); pending.drop(1).forEach { it.await() }; close.await()
        } finally { release.countDown(); runtime.close().await() }
        println("PASS Kotlin JNI session cycles=$cycles concurrent_calls=96 backpressure=256 close_reserved=true")
    }
}
