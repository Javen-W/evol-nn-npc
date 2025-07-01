extends CharacterBody2D
class_name CharacterComponent

# NODE REFERENCES
@export var collision_component : CollisionComponent
@export var sprite_component : SpriteComponent
@export var brain_component: NNBrainComponent
@export var nav_component: NavigationComponent
@export var health_component : HealthComponent
@export var attack_component : AttackComponent
@export var game : Game

# MOVEMENT VARS ---------------- #
@export var max_speed: float = 560
@export var acceleration: float = 2880
@export var turning_acceleration : float = 9600
@export var deceleration: float = 3200
@export var jump_force : float = 1100
var jump_cut : float = 0.25
var jump_gravity_max : float = 500
var jump_hang_treshold : float = 2.0
var jump_hang_gravity_mult : float = 0.1
var jump_coyote : float = 0.08
var jump_buffer : float = 0.1
var gravity_acceleration : float = 3840
var gravity_max : float = 1020
var jump_coyote_timer : float = 0
var jump_buffer_timer : float = 0

# An enum allows us to keep track of valid states.
enum States {IDLE, WALK, JUMP, ATTACK, HIT, DEAD}
@export var state : States = States.IDLE
@export_enum("TeamA", "TeamB") var team

# signals
signal death(character)

func update_state(new_state: States) -> void:
	self.state = new_state
	if new_state == States.IDLE:
		self.sprite_component.update_animation_state("idle")
	elif new_state == States.WALK:
		self.sprite_component.update_animation_state("walk")
	elif new_state == States.ATTACK:
		self.sprite_component.update_animation_state("attack")
	elif new_state == States.HIT:
		self.sprite_component.update_animation_state("hit")
	elif new_state == States.DEAD:
		self.sprite_component.update_animation_state("dead")

func _ready():
	# update names
	for child in get_children():
		child.set_name.call_deferred(name + "_" + child.name)
	
	# add character to groups
	add_to_group("Characters")
	
	# connect to health hit signal
	self.health_component.hit.connect(on_hit)
	# connect to sprite signal
	self.sprite_component.animation_finished.connect(finished_attack)

func _physics_process(delta):
	if self.brain_component != null and self.game != null:
		var input_state = calculate_input_state()
		var decision = self.brain_component.NextMove(input_state) as OutputDecision
		# print(decision)
		move(decision, delta)

func calculate_input_state():
	var input_state = InputState.new()
	
	# my health
	input_state.MyHealth = self.health_component.health / self.health_component.max_health
	
	# my state 
	input_state.MyState = self.state
	
	# my attack cooldown
	if self.attack_component != null:
		input_state.MyAttackCooldown = self.attack_component.cooling_down
	
	# my damage cooldown
	input_state.MyDamageCooldown = self.health_component.invincible
	
	# get enemy
	var enemy = self.game.get_enemy(self)
	if enemy != null:
		
		# enemy state
		input_state.EnemyState = enemy.state
		
		# enemy health
		input_state.EnemyHealth = enemy.health_component.health / enemy.health_component.max_health
		
		# enemy attack cooldown
		if enemy.attack_component != null:
			input_state.EnemyAttackCooldown = enemy.attack_component.cooling_down
		
		# enemy damage cooldown
		input_state.EnemyDamageCooldown = enemy.health_component.invincible
		
		# update navigation to enemy
		var enemy_pos = enemy.global_position
		var nav_ready = self.nav_component.update_target(enemy_pos)
		if  nav_ready and !self.nav_component.finished():
			var my_pos: Vector2 = self.global_position
			var next_pos: Vector2 = self.nav_component.next()
			var next_diff: Vector2 = next_pos - my_pos
			
			# distance to enemy
			input_state.DistanceToEnemy = my_pos.distance_to(enemy_pos)
			
			# next X to enemy
			input_state.NextXToEnemy = next_diff.x
			
			# next Y to enemy
			input_state.NextYToEnemy = next_diff.y
	
	return input_state

func move(decision: OutputDecision, delta: float) -> void:
	# block input if character is dead
	if self.state != States.DEAD:
		
		if self.state == States.IDLE:
			# attack
			attack(decision)
			
		if self.state != States.ATTACK:
			# vertical movement
			jump(decision, delta)
			
			# horizontal movement
			walk(decision, delta)
			
			# idle movement
			idle()
	
	# apply gravity
	gravity(delta)
	
	# update jump timers
	update_movement_timers(delta)
#
	# apply movement and slide from collisions
	move_and_slide()

func finished_attack():
	if self.state == States.ATTACK:
		self.attack_component.stop_attack()
		self.update_state(States.IDLE)

func die():
	# print(name + " died!")
	self.update_state(States.DEAD)
	self.death.emit(self)

func on_hit():
	# check if health is at zero
	if self.health_component.health <= 0.0:
		die()
	if health_component.invincible:
		self.update_state(States.HIT)

func attack(decision: OutputDecision):
	if self.attack_component != null and decision.attack == true:
		if self.attack_component.start_attack():
			self.update_state(States.ATTACK)

func idle():
	# update state to idle if not moving or falling
	if is_on_floor() and self.velocity.x == 0:
		self.update_state(States.IDLE)

func walk(decision: OutputDecision, delta: float):
	# x input
	var x_dir = decision.x

	# decelerate if we're not doing movement inputs
	if x_dir == 0: 
		velocity.x = Vector2(velocity.x, 0).move_toward(Vector2(0,0), deceleration * delta).x
		return
	
	# if above max speed, don't accelerate or decelerate
	if abs(velocity.x) >= max_speed and sign(velocity.x) == x_dir:
		return
	
	# Are we turning? Deciding between acceleration and turn_acceleration
	var accel_rate : float = acceleration if sign(velocity.x) == x_dir else turning_acceleration
	
	# Accelerate
	velocity.x += x_dir * accel_rate * delta
	
	# Change sprite direction
	self.sprite_component.set_direction(x_dir)
	
	# update state to walk if moving but not falling
	if is_on_floor() and self.velocity.x != 0:
		self.update_state(States.WALK)

func jump(decision: OutputDecision, _delta: float) -> void:
	# Reset our jump requirements
	if is_on_floor():
		jump_coyote_timer = jump_coyote
		# is_jumping = false
	if decision.jump:
		jump_buffer_timer = jump_buffer
	
	# Jump if grounded, there is jump input, and we aren't jumping already
	if jump_coyote_timer > 0 and jump_buffer_timer > 0 and self.state != States.JUMP:
		jump_coyote_timer = 0
		jump_buffer_timer = 0
		# If falling, account for that lost speed
		if velocity.y > 0:
			velocity.y -= velocity.y
		velocity.y = -jump_force
		
		# update state to jump
		self.update_state(States.JUMP)
	
	# This way we won't start slowly descending / floating once hit a ceiling
	if is_on_ceiling():
		velocity.y = jump_hang_treshold + 100.0

func gravity(delta: float) -> void:
	var applied_gravity : float = 0
	
	# No gravity if we are grounded
	if jump_coyote_timer > 0:
		return
	
	# Normal gravity limit
	if velocity.y <= gravity_max:
		applied_gravity = gravity_acceleration * delta
	
	# If moving upwards while jumping, the limit is jump_gravity_max to achieve lower gravity
	if (self.state == States.JUMP and velocity.y < 0) and velocity.y > jump_gravity_max:
		applied_gravity = 0
	
	# Lower the gravity at the peak of our jump (where velocity is the smallest)
	if self.state == States.JUMP and abs(velocity.y) < jump_hang_treshold:
		applied_gravity *= jump_hang_gravity_mult
	
	velocity.y += applied_gravity

func update_movement_timers(delta: float) -> void:
	jump_coyote_timer -= delta
	jump_buffer_timer -= delta
